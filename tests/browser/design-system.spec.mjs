import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
// The application is a Skia-rendered Uno UI. Read-only diagnostics locate real
// controls; all interactions below use browser mouse/keyboard/file-picker input.
const control = async (page, name, type) => {
  await expect.poll(() => page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && (!type || c.type === type) && c.enabled), { name, type })).toBe(true);
  return page.evaluate(({ name, type }) => globalThis.__vectorSpaceControls.filter(c => c.name === name && (!type || c.type === type) && c.enabled).at(-1), { name, type });
};
async function click(page, name, type) {
  const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2);
}
async function input(page, name, text) {
  await click(page, name, 'TextBox'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(text);
}
async function choose(page, name, value) {
  await click(page, name, 'ComboBox'); await click(page, value, 'ComboBoxItem');
}
async function openFixture(page) {
  await page.goto('?test=1');
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas');
  await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles('tests/browser/fixtures/design-system.vectorspace');
  await expect.poll(async () => (await state(page)).page).toBe('Design system');
  await page.waitForTimeout(350);
}
async function point(page, x, y, deep = true) {
  const s = await state(page); const c = await control(page, 'Design canvas');
  if (deep) await page.keyboard.down('Control');
  await page.mouse.click(c.x + s.panX + x * s.zoom, c.y + s.panY + y * s.zoom);
  if (deep) await page.keyboard.up('Control');
}
async function findInInspector(page, name, type) {
  for (let i = 0; i < 14; i++) {
    if (await page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && c.type === type), { name, type })) return;
    await page.mouse.move(1560, 700); await page.mouse.wheel(0, 220); await page.waitForTimeout(220);
  }
  throw new Error(`Inspector control not found: ${name}`);
}

test('inherited variable modes change pixels and survive undo, save and reload', async ({ page }) => {
  const errors = []; page.on('pageerror', error => errors.push(error.message));
  await openFixture(page); await point(page, 380, 130);
  await expect.poll(async () => (await state(page)).id).toBe('swatch');
  expect((await state(page)).fill).toBe('#FFFFFF');
  await page.keyboard.press('Escape');
  await findInInspector(page, 'Mode Theme', 'ComboBox');
  await choose(page, 'Mode Theme', 'Dark');
  await point(page, 380, 130);
  await expect.poll(async () => (await state(page)).fill).toBe('#111111');
  const canvas = await control(page, 'Design canvas');
  await page.screenshot({ path: 'artifacts/screenshots/variables-dark.png' });
  await page.keyboard.press('Control+z');
  // Restoring the transaction restores its original (empty) selection.
  await point(page, 380, 130);
  await expect.poll(async () => (await state(page)).fill).toBe('#FFFFFF');
  await page.keyboard.press('Control+Shift+z'); await point(page, 380, 130);
  await expect.poll(async () => (await state(page)).fill).toBe('#111111');
  const download = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const file = await download; await fs.mkdir('artifacts', { recursive: true });
  await file.saveAs('artifacts/variables.vectorspace');
  const saved = JSON.parse(await fs.readFile('artifacts/variables.vectorspace', 'utf8'));
  expect(saved.variableModes.theme).toBe('dark'); expect(saved.formatVersion).toBe(7);
  expect(canvas.width).toBeGreaterThan(400);
  await page.waitForTimeout(1400); await page.reload();
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready); await control(page, 'Design canvas');
  await point(page, 380, 130); await expect.poll(async () => (await state(page)).fill).toBe('#111111');
  expect(errors).toEqual([]);
});

test('variant inspector swaps a real instance and undo restores its source', async ({ page }) => {
  await openFixture(page); await point(page, 324, 304);
  await expect.poll(async () => (await state(page)).id).toBe('button-instance');
  expect((await state(page)).componentId).toBe('variant-default');
  await findInInspector(page, 'Variant State', 'ComboBox'); await choose(page, 'Variant State', 'Active');
  await expect.poll(async () => (await state(page)).componentId).toBe('variant-active');
  expect((await state(page)).fill).toBe('#14AE5C');
  await page.screenshot({ path: 'artifacts/screenshots/variant-properties.png' });
  await point(page, 324, 304); await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).componentId).toBe('variant-default');
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).componentId).toBe('variant-active');
});

test('local variable editor creates a collection, mode and typed variable through controls', async ({ page }) => {
  await openFixture(page); await page.mouse.click(650, 240); await page.keyboard.press('Control+k');
  await input(page, 'Search quick actions', 'Local variables'); await click(page, 'Local variables');
  await input(page, 'New collection name', 'Spacing'); await click(page, 'New collection');
  await expect.poll(async () => (await state(page)).collections).toBe(2);
  await input(page, 'New mode name', 'Comfortable'); await click(page, 'Add mode');
  await input(page, 'New variable name', 'Gap'); await choose(page, 'New variable type', 'Number'); await click(page, 'Create');
  await expect.poll(async () => (await state(page)).variables).toBe(2);
  await page.screenshot({ path: 'artifacts/screenshots/local-variables.png' });
  await click(page, 'Close'); await point(page, 380, 130); await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).variables).toBe(1);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).variables).toBe(2);
});

test('system clipboard carries variable dependencies between different documents', async ({ page, context }) => {
  await openFixture(page);
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await point(page, 380, 130);
  await expect.poll(async () => (await state(page)).id).toBe('swatch');
  await page.keyboard.press('Control+c');
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toContain('VectorSpace/1\n');
  const copied = await page.evaluate(() => navigator.clipboard.readText());
  const envelope = JSON.parse(copied.slice(copied.indexOf('\n') + 1));
  expect(envelope.variables).toHaveLength(1);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'target.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({ formatVersion: 2, id: 'clipboard-target', name: 'Clipboard target', pages: [{ id: 'target-page', name: 'Target', nodes: [] }] })) });
  await expect.poll(async () => (await state(page)).page).toBe('Target');
  expect((await state(page)).variables).toBe(0);
  await page.mouse.click(650, 240); await page.keyboard.press('Control+v');
  await expect.poll(async () => (await state(page)).variables).toBe(1);
  expect((await state(page)).bindings).toBe(1);
  expect((await state(page)).fill).toBe('#FFFFFF');
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).variables).toBe(0);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).bindings).toBe(1);
});
