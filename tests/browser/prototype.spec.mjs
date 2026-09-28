import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function control(page, name, type) {
  await expect.poll(() => page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && (!type || c.type === type) && c.enabled), { name, type })).toBe(true);
  return page.evaluate(({ name, type }) => globalThis.__vectorSpaceControls.filter(c => c.name === name && (!type || c.type === type) && c.enabled).at(-1), { name, type });
}
async function click(page, name, type) { const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
async function choose(page, name, value) {
  await click(page, name, 'ComboBox'); await click(page, value, 'ComboBoxItem');
  // Changing action kind rebuilds the inspector. A preceding diagnostics sample
  // still has clickable bounds for the detached destination control.
  await expect.poll(() => page.evaluate(({ name }) => globalThis.__vectorSpaceControls?.filter(c => c.name === name && c.type === 'ComboBox').at(-1)?.value, { name })).toBe(value);
}
function fixture() {
  const reaction = (...actions) => ({ actions });
  const hot = (id, x, y, actions) => ({ id, name: id, kind: 'Rectangle', x, y, width: 100, height: 50, fills: [{ color: '#0D99FF' }], reactions: [reaction(...actions)] });
  const a = { id: 'a', name: 'Welcome', kind: 'Frame', width: 400, height: 300, clipContent: true, prototypeFlowName: 'Main flow', prototypeOverflow: 'Vertical', fills: [{ color: '#FFFFFF' }], children: [
    hot('go', 20, 20, [{ kind: 'Navigate', targetId: 'b', transition: { kind: 'Dissolve', durationMilliseconds: 250 } }]),
    hot('open', 20, 100, [{ kind: 'OpenOverlay', targetId: 'modal' }]),
    hot('toggle', 20, 180, [{ kind: 'SetVariable', variableId: 'flag', operation: 'Toggle' }, { kind: 'Conditional', condition: { variableId: 'flag', value: { type: 'Boolean', boolean: true } }, then: [{ kind: 'Navigate', targetId: 'b' }], else: [{ kind: 'OpenOverlay', targetId: 'modal' }] }]),
    hot('jump', 200, 20, [{ kind: 'ScrollTo', targetId: 'bottom' }]),
    { id: 'bottom', name: 'Footer', kind: 'Rectangle', x: 0, y: 800, width: 400, height: 100, fills: [{ color: '#14AE5C' }] }
  ], reactions: [{ trigger: 'KeyDown', key: 'K', actions: [{ kind: 'Navigate', targetId: 'b' }] }] };
  const b = { id: 'b', name: 'Detail', kind: 'Frame', x: 600, width: 400, height: 300, fills: [{ color: '#E5F5FF' }], children: [hot('back', 20, 20, [{ kind: 'Back' }])] };
  const modal = { id: 'modal', name: 'Modal', kind: 'Frame', x: 1200, width: 200, height: 100, fills: [{ color: '#F4EAFF' }], children: [hot('close', 20, 20, [{ kind: 'CloseOverlay' }])] };
  return { formatVersion: 3, id: 'prototype-fixture', name: 'Prototype acceptance', pages: [{ id: 'page', name: 'Prototype test', nodes: [a, b, modal] }], variableCollections: [{ id: 'collection', defaultModeId: 'default', modes: [{ id: 'default', name: 'Default' }] }], variables: [{ id: 'flag', name: 'Enabled', collectionId: 'collection', type: 'Boolean', values: { default: { type: 'Boolean', boolean: false } } }] };
}
async function open(page, doc = fixture()) {
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'prototype.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(doc)) });
  await expect.poll(async () => (await state(page)).page).toBe('Prototype test');
  await designPoint(page, 350, 270); await expect.poll(async () => (await state(page)).id).toBe('a');
}
async function designPoint(page, x, y) {
  const c = await control(page, 'Design canvas'); const s = await state(page);
  await page.keyboard.down('Control'); await page.mouse.click(c.x + s.panX + x * s.zoom, c.y + s.panY + y * s.zoom); await page.keyboard.up('Control');
}
async function coordinates(page, x, y) {
  const c = await control(page, 'Prototype canvas'); const p = (await state(page)).prototype;
  return { x: c.x + p.panX + x * p.zoom, y: c.y + p.panY + y * p.zoom };
}
async function point(page, x, y) { const p = await coordinates(page, x, y); await page.mouse.click(p.x, p.y); }
async function present(page) { await click(page, 'Present prototype'); await expect.poll(async () => (await state(page)).presenting).toBe(true); await control(page, 'Prototype canvas'); await expect.poll(async () => (await state(page)).prototype?.frame).toBe('a'); }

test('prototype clicks fire on release, back navigates and exit preserves editor viewport', async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message)); await open(page);
  const before = await state(page); await present(page);
  const go = await coordinates(page, 60, 45); await page.mouse.move(go.x, go.y); await page.mouse.down();
  await page.waitForTimeout(250); expect((await state(page)).prototype.frame).toBe('a');
  await page.mouse.up(); await expect.poll(async () => (await state(page)).prototype.frame).toBe('b');
  await expect.poll(async () => (await state(page)).prototype.animating).toBe(false);
  await click(page, 'Prototype back'); await expect.poll(async () => (await state(page)).prototype.frame).toBe('a');
  await page.screenshot({ path: 'artifacts/screenshots/prototype-player.png' });
  await page.keyboard.press('Escape'); await expect.poll(async () => (await state(page)).presenting).toBe(false);
  const after = await state(page); for (const key of ['history', 'zoom', 'panX', 'panY', 'id']) expect(after[key]).toBe(before[key]); expect(errors).toEqual([]);
});

test('prototype overlays consume outside clicks and Escape dismisses before exiting', async ({ page }) => {
  await open(page); await present(page); await point(page, 60, 125);
  await expect.poll(async () => (await state(page)).prototype.overlays).toBe(1);
  await page.screenshot({ path: 'artifacts/screenshots/prototype-overlay.png' });
  await point(page, 60, 45); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(0);
  expect((await state(page)).prototype.frame).toBe('a');
  await point(page, 60, 125); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(1);
  await page.keyboard.press('Escape'); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(0); expect((await state(page)).presenting).toBe(true);
  await point(page, 60, 125); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(1);
  await point(page, 150, 145); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(0);
  await click(page, 'Close prototype'); await expect.poll(async () => (await state(page)).presenting).toBe(false);
});

test('prototype variable branches are isolated and Restart resets runtime state', async ({ page }) => {
  await open(page); const before = await state(page); await present(page); await point(page, 60, 205);
  await expect.poll(async () => (await state(page)).prototype.frame).toBe('b'); expect((await state(page)).prototype.values.flag).toBe('True');
  await click(page, 'Prototype back'); await expect.poll(async () => (await state(page)).prototype.frame).toBe('a');
  await point(page, 60, 205); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(1); expect((await state(page)).prototype.values.flag).toBe('False');
  await click(page, 'Prototype restart'); await expect.poll(async () => (await state(page)).prototype.overlays).toBe(0); expect((await state(page)).prototype.frame).toBe('a');
  await point(page, 60, 205); await expect.poll(async () => (await state(page)).prototype.values.flag).toBe('True');
  await click(page, 'Close prototype'); await expect.poll(async () => (await state(page)).presenting).toBe(false); expect((await state(page)).history).toBe(before.history);
  const download = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const saved = await download; await fs.mkdir('artifacts', { recursive: true }); await saved.saveAs('artifacts/prototype-isolation.vectorspace');
  const doc = JSON.parse(await fs.readFile('artifacts/prototype-isolation.vectorspace', 'utf8')); expect(doc.variables[0].values.default.boolean).toBe(false);
});

test('prototype scroll-to and wheel use clipped frame coordinates and keyboard navigation', async ({ page }) => {
  await open(page); await present(page); await point(page, 250, 45);
  await expect.poll(async () => (await state(page)).prototype.scrollY).toBe(600);
  const p = await coordinates(page, 200, 200); await page.mouse.move(p.x, p.y); await page.mouse.wheel(0, -180);
  await expect.poll(async () => (await state(page)).prototype.scrollY).toBeLessThan(600);
  const savedScroll = (await state(page)).prototype.scrollY;
  expect(savedScroll).toBeGreaterThan(0);
  await page.keyboard.press('k'); await expect.poll(async () => (await state(page)).prototype.frame).toBe('b');
  await click(page, 'Prototype back');
  // Diagnostics are published on a timer. Wait for the returned frame before
  // comparing its exact saved offset; the destination frame has scroll zero.
  await expect.poll(async () => (await state(page)).prototype.frame).toBe('a');
  await expect.poll(async () => (await state(page)).prototype.scrollY).toBeCloseTo(savedScroll, 4);
});

test('prototype inspector authors actions and saves schema v3 without a mutation API', async ({ page }) => {
  await open(page); await designPoint(page, 60, 45); await expect.poll(async () => (await state(page)).id).toBe('go');
  await click(page, 'Prototype'); await choose(page, 'Action type', 'OpenOverlay'); await choose(page, 'Prototype destination', 'Modal');
  const beforeSave = await state(page); const download = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const saved = await download; await saved.saveAs('artifacts/prototype-authored.vectorspace');
  const doc = JSON.parse(await fs.readFile('artifacts/prototype-authored.vectorspace', 'utf8')); const action = doc.pages[0].nodes[0].children.find(n => n.id === 'go').reactions[0].actions[0];
  expect(doc.formatVersion).toBe(4); expect(action.kind).toBe('OpenOverlay'); expect(action.targetId).toBe('modal'); expect(beforeSave.history).toBeGreaterThan(0);
  await page.screenshot({ path: 'artifacts/screenshots/prototype-inspector.png' });
});

test('prototype delay navigation runs automatically and stops when the player closes', async ({ page }) => {
  const d = fixture(); d.pages[0].nodes[0].reactions = [{ trigger: 'AfterDelay', delayMilliseconds: 500, actions: [{ kind: 'Navigate', targetId: 'b' }] }];
  await open(page, d); await present(page); await expect.poll(async () => (await state(page)).prototype.frame).toBe('b');
  await click(page, 'Close prototype'); await expect.poll(async () => (await state(page)).presenting).toBe(false); const after = await state(page);
  await page.waitForTimeout(700); expect((await state(page)).history).toBe(after.history); expect((await state(page)).presenting).toBe(false);
});
