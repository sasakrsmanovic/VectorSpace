import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
import { pixels } from './png-pixels.mjs';

const image = 'iVBORw0KGgoAAAANSUhEUgAAAPAAAAB4CAIAAABD1OhwAAABXElEQVR4nO3SgQkAIADDMPX/n/UJUSjJBWN07sELc3j6hfV7ANwkaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkCJoUQZMiaFIETYqgSRE0KYImRdCkHCESAu8dQUSQAAAAAElFTkSuQmCC';
const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function control(page, name, type) {
  await expect.poll(() => page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && (!type || c.type === type) && c.enabled), { name, type })).toBe(true);
  return page.evaluate(({ name, type }) => globalThis.__vectorSpaceControls.filter(c => c.name === name && (!type || c.type === type) && c.enabled).at(-1), { name, type });
}
async function click(page, name, type) { const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
async function find(page, name, type) {
  await page.mouse.move(1570, 700); await page.mouse.wheel(0, -8000); await page.waitForTimeout(250);
  for (let i = 0; i < 30; i++) {
    if (await page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && (!type || c.type === type) && c.enabled && c.height > 12), { name, type })) return;
    await page.mouse.wheel(0, 190); await page.waitForTimeout(220);
  }
  throw new Error(`Inspector control not found: ${name}`);
}
async function choose(page, name, value) { await find(page, name, 'ComboBox'); await click(page, name, 'ComboBox'); await click(page, value, 'ComboBoxItem'); }
async function enter(page, name, value) { await find(page, name, 'TextBox'); await click(page, name, 'TextBox'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(String(value)); await page.keyboard.press('Enter'); }
async function screen(page, x, y) { const c = await control(page, 'Design canvas'); const s = await state(page); return { x: c.x + s.panX + x * s.zoom, y: c.y + s.panY + y * s.zoom }; }
async function point(page, x, y, deep = false) { const p = await screen(page, x, y); if (deep) await page.keyboard.down('Control'); await page.mouse.click(p.x, p.y); if (deep) await page.keyboard.up('Control'); }
function fixture(fill = { kind: 'Image', imageData: 'data:image/png;base64,' + image }) {
  return { formatVersion: 4, name: 'Appearance acceptance', pages: [{ id: 'appearance-page', name: 'Appearance', nodes: [
    { id: 'stage', kind: 'Frame', width: 800, height: 600, fills: [{ color: '#FFFFFF' }], children: [
      { id: 'target', name: 'Image layer', x: 200, y: 170, width: 240, height: 240, fills: [fill] }
    ] }
  ] }] };
}
async function open(page, file = { name: 'appearance.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(fixture())) }) {
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await (await picker).setFiles(file);
  await expect.poll(async () => (await state(page)).page).toBe('Appearance');
  await page.waitForTimeout(500);
}
async function select(page) { await point(page, 320, 290, true); await expect.poll(async () => (await state(page)).id).toBe('target'); }
async function save(page, filename) {
  const promise = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download = await promise; await fs.mkdir('artifacts', { recursive: true }); const path = 'artifacts/' + filename; await download.saveAs(path);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}

test('image picker places an editable self-contained layer with undo and redo', async ({ page }) => {
  await open(page); await expect.poll(async () => (await state(page)).page).toBe('Appearance'); await point(page, 700, 500);
  const before = await state(page), picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+Shift+k');
  await (await picker).setFiles({ name: 'Original image.png', mimeType: 'image/png', buffer: Buffer.from(image, 'base64') });
  await expect.poll(async () => (await state(page)).nodes).toBe(before.nodes + 1);
  const added = await state(page); expect(added.name).toBe('Original image'); expect(added.width).toBe(240); expect(added.height).toBe(120); expect(added.imageMode).toBe('Fill');
  const doc = await save(page, 'image-import.vectorspace'); expect(doc.formatVersion).toBe(4);
  const layer = doc.pages[0].nodes.find(n => n.id === added.id); expect(layer.fills[0].imageData).toMatch(/^data:image\/png;base64,/);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).nodes).toBe(before.nodes);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).id).toBe(added.id);
});

test('image Fit changes actual browser pixels and persists its mode', async ({ page }) => {
  await open(page); await select(page); await choose(page, 'Image scale mode', 'Fit');
  await expect.poll(async () => (await state(page)).imageMode).toBe('Fit'); await page.waitForTimeout(250);
  const get = pixels(await page.screenshot({ path: 'artifacts/screenshots/image-fit.png' }));
  const top = await screen(page, 240, 190), red = await screen(page, 240, 290), blue = await screen(page, 400, 290);
  expect(get(top.x, top.y)).toEqual([255, 255, 255]); expect(get(red.x, red.y)[0]).toBeGreaterThan(240); expect(get(red.x, red.y)[2]).toBeLessThan(10);
  expect(get(blue.x, blue.y)[2]).toBeGreaterThan(240);
  await point(page, 320, 290, true); const doc = await save(page, 'image-fit.vectorspace'); expect(doc.pages[0].nodes[0].children[0].fills[0].imageMode).toBe('Fit');
});

test('on-canvas crop is baseline-relative, wheel anchored and cancels without moving the layer', async ({ page }) => {
  // Fill ignores these retained crop offsets. Entering crop mode must preserve
  // the visible placement instead of suddenly reactivating the old offsets.
  const document = fixture({ kind: 'Image', imageData: 'data:image/png;base64,' + image, imageMode: 'Fill', imageOffset: { x: .75, y: -.5 } });
  await open(page, { name: 'latent-crop.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(document)) });
  await select(page); await find(page, 'Edit image crop'); await click(page, 'Edit image crop');
  await expect.poll(async () => (await state(page)).cropping).toBe(true);
  await expect.poll(async () => (await state(page)).cropX).toBe(0);
  await expect.poll(async () => (await state(page)).cropY).toBe(0);
  const a = await screen(page, 320, 290), b = await screen(page, 350, 302);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 12 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).cropX).toBeCloseTo(.125, 2); await expect.poll(async () => (await state(page)).cropY).toBeCloseTo(.05, 2);
  const before = await state(page); const cursor = await screen(page, 300, 260); await page.mouse.move(cursor.x, cursor.y); await page.mouse.wheel(0, -120);
  await expect.poll(async () => (await state(page)).imageScale).toBeGreaterThan(before.imageScale);
  const after = await state(page), ratio = after.imageScale / before.imageScale;
  // Fixed cursor local coordinates: newCenter + ratio * (cursor - oldCenter) == cursor.
  expect(120 + after.cropX * 240 + ratio * (100 - (120 + before.cropX * 240))).toBeCloseTo(100, 1);
  expect(120 + after.cropY * 240 + ratio * (90 - (120 + before.cropY * 240))).toBeCloseTo(90, 1);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 5 }); await page.keyboard.press('Escape'); await page.mouse.up();
  await expect.poll(async () => (await state(page)).cropping).toBe(false);
  expect((await state(page)).cropX).toBeCloseTo(after.cropX, 5); expect((await state(page)).x).toBe(200); expect((await state(page)).y).toBe(170);
  await find(page, 'Edit image crop'); await click(page, 'Edit image crop'); await page.screenshot({ path: 'artifacts/screenshots/image-crop.png' });
  await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).cropping).toBe(false);
  const doc = await save(page, 'image-crop.vectorspace'); expect(doc.pages[0].nodes[0].children[0].fills[0].imageOffset.x).toBeCloseTo(after.cropX, 5);
});

test('effect inspector authors a full stack and persists inner-shadow spread and layer blur', async ({ page }) => {
  await open(page, { name: 'effects.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(fixture({ color: '#14AE5C' }))) }); await select(page);
  await find(page, 'Effects options'); await click(page, 'Effects options'); await expect.poll(async () => (await state(page)).effects).toBe(1);
  await choose(page, 'Effect type 1', 'InnerShadow'); await enter(page, 'Spread', 8);
  await find(page, 'Effects options'); await click(page, 'Effects options'); await expect.poll(async () => (await state(page)).effects).toBe(2);
  await choose(page, 'Effect type 2', 'LayerBlur');
  await point(page, 320, 290, true); const doc = await save(page, 'effect-stack.vectorspace'); const effects = doc.pages[0].nodes[0].children[0].shadows;
  expect(effects).toHaveLength(2); expect(effects[0].kind).toBe('InnerShadow'); expect(effects[0].spread).toBe(8); expect(effects[1].kind).toBe('LayerBlur');
  await page.screenshot({ path: 'artifacts/screenshots/effect-stack.png' });
  await page.keyboard.press('Control+z'); await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).effects).toBe(1);
});

test('SVG gradient imports retain editable stops and browser colors', async ({ page }) => {
  const svg = `<svg width="800" height="600"><defs><linearGradient id="base"><stop stop-color="#ff0000"/><stop offset="1" stop-color="#0000ff"/></linearGradient><linearGradient id="derived" href="#base"/></defs><rect x="200" y="170" width="240" height="240" fill="url(#derived)"/></svg>`;
  await open(page); await point(page, 700, 500);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'gradient.svg', mimeType: 'image/svg+xml', buffer: Buffer.from(svg) });
  await expect.poll(async () => (await state(page)).name).toBe('gradient');
  const frame = await state(page); await point(page, frame.x + 320, frame.y + 290, true); await page.waitForTimeout(300);
  const sample = pixels(await page.screenshot({ path: 'artifacts/screenshots/svg-gradient.png' }));
  const left = await screen(page, frame.x + 210, frame.y + 290), right = await screen(page, frame.x + 430, frame.y + 290);
  expect(sample(left.x, left.y)[0]).toBeGreaterThan(230); expect(sample(right.x, right.y)[2]).toBeGreaterThan(230);
  const doc = await save(page, 'gradient-import.vectorspace'); const fill = doc.pages[0].nodes.find(n => n.name === 'gradient').children[0].fills[0];
  expect(fill.kind).toBe('LinearGradient'); expect(fill.stops).toHaveLength(2); expect(fill.stops[0].color.toLowerCase()).toBe('#ff0000');
});

test('appearance playground opens through quick actions and renders the original material gallery', async ({ page }) => {
  await open(page); await point(page, 700, 500); await page.keyboard.press('Control+k');
  await click(page, 'Search quick actions', 'TextBox'); await page.keyboard.insertText('Appearance playground');
  await click(page, 'Appearance playground'); await click(page, 'Continue');
  await expect.poll(async () => (await state(page)).page).toBe('Material studies');
  await page.waitForTimeout(400); await page.screenshot({ path: 'artifacts/screenshots/appearance-playground.png' });
  expect((await state(page)).imageDecodes).toBeGreaterThan(0); expect((await state(page)).nodes).toBeGreaterThan(10);
});
