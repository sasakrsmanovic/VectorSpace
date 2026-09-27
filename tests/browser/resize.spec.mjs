import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function canvas(page) {
  await expect.poll(() => page.evaluate(() => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === 'Design canvas'))).toBe(true);
  return page.evaluate(() => globalThis.__vectorSpaceControls.find(c => c.name === 'Design canvas'));
}
async function screen(page, x, y) {
  const [view, scene] = await Promise.all([canvas(page), state(page)]);
  return { x: view.x + scene.panX + x * scene.zoom, y: view.y + scene.panY + y * scene.zoom };
}
async function open(page, target) {
  await page.goto('?test=1');
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await canvas(page); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'resize.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({
    formatVersion: 2, id: 'resize-document', name: 'Resize acceptance', pages: [{ id: 'resize-page', name: 'Resize', nodes: [{
      id: 'stage', kind: 'Frame', width: 800, height: 600, fills: [{ color: '#FAFAFA' }], children: [target]
    }] }]
  })) });
  await expect.poll(async () => (await state(page)).page).toBe('Resize');
  await page.waitForTimeout(350);
}
async function select(page, x, y) {
  const p = await screen(page, x, y); await page.keyboard.down('Control'); await page.mouse.click(p.x, p.y); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).id).toBe('target');
}
async function drag(page, from, to) {
  const a = await screen(page, ...from); const b = await screen(page, ...to);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 12 }); await page.mouse.up();
}

test('side resize preserves hug height, reflows wrap, and saves the sizing mode', async ({ page }) => {
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  await open(page, { id: 'target', name: 'Hug-height layout', kind: 'Frame', x: 300, y: 260, width: 210, height: 40,
    layout: { direction: 'Horizontal', wrap: true, hugHeight: true, gap: 10, crossGap: 8, paddingLeft: 0, paddingRight: 0, paddingTop: 0, paddingBottom: 0 },
    children: [{ id: 'first', width: 100, height: 40, fills: [{ color: '#9B8AFB' }] }, { id: 'second', x: 110, width: 100, height: 40, fills: [{ color: '#14AE5C' }] }]
  });
  await select(page, 405, 280); const before = await state(page);
  await drag(page, [510, 280], [400, 280]);
  await expect.poll(async () => (await state(page)).width).toBeCloseTo(100, 0);
  await expect.poll(async () => (await state(page)).height).toBeCloseTo(88, 0);
  expect((await state(page)).y).toBeCloseTo(260, 0);
  expect((await state(page)).history).toBe(before.history + 1);
  const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const saved = await pending; await fs.mkdir('artifacts', { recursive: true }); await saved.saveAs('artifacts/resize.vectorspace');
  const document = JSON.parse(await fs.readFile('artifacts/resize.vectorspace', 'utf8'));
  expect(document.pages[0].nodes[0].children[0].layout.hugHeight).toBe(true);
  await page.screenshot({ path: 'artifacts/screenshots/resize-hug-height.png' });
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).width).toBeCloseTo(210, 0);
  expect((await state(page)).height).toBeCloseTo(40, 0);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).height).toBeCloseTo(88, 0);
  expect(errors).toEqual([]);
});

test('clamped west resize fixes the opposite edge and Alt Shift fixes the center', async ({ page }) => {
  await open(page, { id: 'target', name: 'Constrained rectangle', kind: 'Rectangle', x: 300, y: 260,
    width: 100, height: 40, minWidth: 80, maxWidth: 150, fills: [{ color: '#0D99FF' }] });
  await select(page, 350, 280);
  await drag(page, [300, 280], [399, 280]);
  await expect.poll(async () => (await state(page)).width).toBe(80);
  expect((await state(page)).x).toBeCloseTo(320, 3);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).width).toBe(100);
  await page.keyboard.down('Alt'); await page.keyboard.down('Shift');
  await drag(page, [300, 260], [100, 60]);
  await page.keyboard.up('Shift'); await page.keyboard.up('Alt');
  await expect.poll(async () => (await state(page)).width).toBe(150);
  const result = await state(page); expect(result.height).toBe(60);
  expect(result.x + result.width / 2).toBeCloseTo(350, 3); expect(result.y + result.height / 2).toBeCloseTo(280, 3);
});
