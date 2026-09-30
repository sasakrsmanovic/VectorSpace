import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function control(page, name, type) {
  let found;
  await expect.poll(async () => {
    found = await page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).filter(c => c.name === name && c.enabled && (!type || c.type === type)).at(-1), { name, type });
    return !!found;
  }).toBe(true);
  return found;
}
async function click(page, name, type) { const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
async function enter(page, name, text) { await click(page, name, 'TextBox'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(text); }
async function select(page, id, shift = false) {
  const locations = { a: [160, 130], b: [440, 130], c: [650, 130], sourceText: [120, 345], targetText: [440, 345] };
  const [x, y] = locations[id]; const canvas = await control(page, 'Design canvas'); const s = await state(page);
  await page.keyboard.down('Control'); if (shift) await page.keyboard.down('Shift');
  await page.mouse.click(canvas.x + s.panX + x * s.zoom, canvas.y + s.panY + y * s.zoom);
  if (shift) await page.keyboard.up('Shift'); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).id).toBe(id);
}
async function action(page, name) {
  await page.keyboard.press('Control+k'); await enter(page, 'Search quick actions', name);
  // Results existed in the unfiltered palette too. Allow diagnostics to observe the
  // newly filtered layout, then use the same observed bounds for the real click.
  await page.waitForTimeout(450); await click(page, name);
  await expect.poll(() => page.evaluate(() => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === 'Search quick actions'))).toBe(false);
}
function fixture() {
  return { formatVersion: 4, id: 'editing-workflows', name: 'Editing workflows', pages: [{ id: 'workflow-page', name: 'Workflows', nodes: [{
    id: 'stage', kind: 'Frame', width: 800, height: 600, fills: [{ color: '#FFFFFF' }], children: [
      { id: 'a', name: 'Icon_001', x: 80, y: 80, width: 160, height: 100, cornerRadius: 18, opacity: .75, blend: 'Multiply',
        fills: [{ color: '#F24822' }], strokes: [{ color: '#14AE5C', width: 3, dashes: [0, 4] }], shadows: [{ x: 0, y: 8, blur: 12, opacity: .3 }] },
      { id: 'b', name: 'Icon_002', x: 360, y: 80, width: 160, height: 100, fills: [{ color: '#0D99FF' }] },
      { id: 'c', name: 'Icon_003', x: 600, y: 80, width: 100, height: 100, fills: [{ color: '#9747FF' }] },
      { id: 'sourceText', name: 'Typography source', kind: 'Text', text: 'Typography source', x: 80, y: 320, width: 280, height: 70,
        fontFamily: 'Inter', fontSize: 28, fontWeight: 600, lineHeight: 1.6, letterSpacing: 1.25, textAlign: 'Left', fills: [{ color: '#F24822' }] },
      { id: 'targetText', name: 'Typography target', kind: 'Text', text: 'Keep this content', x: 400, y: 320, width: 240, height: 70,
        fontFamily: 'Inter', fontSize: 18, fontWeight: 400, fills: [{ color: '#0D99FF' }] }
    ]
  }] }] };
}
async function open(page) {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(700, 240);
  const chooser = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await chooser).setFiles({ name: 'workflows.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(fixture())) });
  await expect.poll(async () => (await state(page)).page).toBe('Workflows'); await page.waitForTimeout(300);
}
async function copied(page) {
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toMatch(/^VectorSpace\.Properties\/1\n/);
}
async function save(page, name) {
  const download = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const file = await download;
  await fs.mkdir('artifacts', { recursive: true }); const path = `artifacts/${name}.vectorspace`; await file.saveAs(path);
  await expect.poll(async () => (await state(page)).saving).toBe(false);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
const node = (document, id) => document.pages[0].nodes[0].children.find(n => n.id === id);

test('property shortcuts transfer styles without changing geometry and undo atomically', async ({ page }) => {
  await open(page); await select(page, 'a'); await page.keyboard.press('Control+Alt+c'); await copied(page);
  const clipboard = await page.evaluate(() => navigator.clipboard.readText());
  expect(clipboard).not.toContain('"children"'); expect(clipboard).not.toContain('"text"');
  await select(page, 'b'); await page.keyboard.press('Control+Alt+v');
  await expect.poll(async () => (await state(page)).fill).toBe('#F24822');
  const result = await save(page, 'property-shortcuts'), a = node(result, 'a'), b = node(result, 'b');
  expect(result.formatVersion).toBe(7); expect(b.fills).toEqual(a.fills); expect(b.strokes).toEqual(a.strokes); expect(b.shadows).toEqual(a.shadows);
  expect(b.opacity).toBe(.75); expect(b.blend).toBe('Multiply'); expect(b.cornerRadius).toBe(18);
  expect([b.x, b.y, b.width, b.height, b.name]).toEqual([360, 80, 160, 100, 'Icon_002']);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).fill).toBe('#0D99FF');
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).fill).toBe('#F24822');
});

test('selective property chooser pastes only requested groups and preserves unrelated data', async ({ page }) => {
  await open(page); await select(page, 'a'); await page.keyboard.press('Control+Alt+c'); await copied(page); await select(page, 'b');
  await action(page, 'Paste selected properties');
  await click(page, 'Transfer Fills', 'CheckBox'); await click(page, 'Transfer Appearance', 'CheckBox');
  await page.screenshot({ path: 'artifacts/screenshots/property-transfer.png' });
  await click(page, 'Apply properties'); await expect.poll(async () => (await state(page)).effects).toBe(1);
  const b = node(await save(page, 'selected-properties'), 'b');
  expect(b.fills[0].color).toBe('#0D99FF'); expect(b.opacity).toBe(1); expect(b.cornerRadius).toBe(0);
  expect(b.strokes[0].dashes).toEqual([0, 4]); expect(b.shadows).toHaveLength(1);
});

test('typography-only transfer leaves text content paints and sizing unchanged', async ({ page }) => {
  await open(page); await select(page, 'sourceText'); await action(page, 'Copy typography'); await copied(page);
  await select(page, 'targetText'); const history = (await state(page)).history; await action(page, 'Paste typography');
  await expect.poll(async () => (await state(page)).history).toBe(history + 1);
  const n = node(await save(page, 'typography-properties'), 'targetText');
  expect([n.fontSize, n.fontWeight, n.lineHeight, n.letterSpacing]).toEqual([28, 600, 1.6, 1.25]);
  expect(n.text).toBe('Keep this content'); expect(n.fills[0].color).toBe('#0D99FF'); expect([n.x, n.y, n.width, n.height]).toEqual([400, 320, 240, 70]);
});

test('batch rename previews numbering in layer order and preserves all IDs through undo', async ({ page }) => {
  await open(page); await select(page, 'a'); await select(page, 'b', true); await select(page, 'c', true);
  await expect.poll(async () => (await state(page)).selection).toBe(3); await page.keyboard.press('F2');
  await enter(page, 'Rename to', 'Card $nn — $&'); await control(page, 'Rename', 'Button');
  await page.screenshot({ path: 'artifacts/screenshots/batch-rename.png' }); await click(page, 'Rename', 'Button');
  await expect.poll(async () => (await state(page)).name).toBe('Card 01 — Icon_003');
  const result = await save(page, 'batch-rename');
  expect(['a', 'b', 'c'].map(id => node(result, id).name)).toEqual(['Card 03 — Icon_001', 'Card 02 — Icon_002', 'Card 01 — Icon_003']);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).history).toBe(0);
  const before = await save(page, 'batch-rename-undo'); expect(['a', 'b', 'c'].map(id => node(before, id).name)).toEqual(['Icon_001', 'Icon_002', 'Icon_003']);
});

test('regex rename uses captures and stable layer ordering preserves relative selected order', async ({ page }) => {
  await open(page); await select(page, 'b'); await select(page, 'c', true); await page.keyboard.press('F2');
  await enter(page, 'Match layer names', '(Icon)_(\\d+)'); await click(page, 'Regular expression', 'CheckBox');
  await enter(page, 'Rename to', '$2/$1'); await click(page, 'Rename', 'Button');
  await expect.poll(async () => (await state(page)).name).toBe('003/Icon');
  await action(page, 'Send to back');
  const result = await save(page, 'stable-layer-order'); expect(result.pages[0].nodes[0].children.map(n => n.id)).toEqual(['b', 'c', 'a', 'sourceText', 'targetText']);
  expect(node(result, 'b').name).toBe('002/Icon'); expect(node(result, 'c').name).toBe('003/Icon');
  await page.keyboard.press('Control+z');
  const restored = await save(page, 'stable-order-undo'); expect(restored.pages[0].nodes[0].children.map(n => n.id)).toEqual(['a', 'b', 'c', 'sourceText', 'targetText']);
});
