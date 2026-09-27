import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const state = page => page.evaluate(() => globalThis.__vectorSpaceState);

async function ready(page) {
  await page.goto('?test=1');
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150_000 });
  await expect.poll(async () => (await state(page)).canvasWidth).toBeGreaterThan(100);
  await page.waitForTimeout(1200);
}

test('real Uno canvas: draw, nudge, undo, redo, delete, save and recover', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') console.log('BROWSER ERROR:', message.text()); });
  await ready(page);
  await fs.mkdir('artifacts/screenshots', { recursive: true });
  await page.screenshot({ path: 'artifacts/screenshots/workbench-desktop.png' });
  const initial = await state(page);
  expect(initial.nodes).toBeGreaterThan(50);
  expect(initial.pages).toBe(3);
  await page.mouse.click(650, 240);
  await page.keyboard.press('r');
  await expect.poll(async () => (await state(page)).tool).toBe('Rectangle');
  await page.mouse.move(650, 300); await page.mouse.down();
  await page.mouse.move(810, 420, { steps: 12 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 1);
  const rectangle = await state(page);
  expect(rectangle.selection).toBe(1);
  expect(rectangle.kind).toBe('Rectangle');
  expect(rectangle.width).toBeGreaterThan(50);
  await page.keyboard.press('ArrowRight');
  await expect.poll(async () => (await state(page)).x).toBeCloseTo(rectangle.x + 1, 3);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).x).toBeCloseTo(rectangle.x, 3);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes);
  await page.keyboard.press('Control+Shift+z');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 1);
  await page.keyboard.press('Control+d');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 2);
  await page.keyboard.press('Delete');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 1);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).selection).toBe(1);
  await page.screenshot({ path: 'artifacts/screenshots/workbench-inspector.png' });
  const downloadPromise = page.waitForEvent('download');
  await page.keyboard.press('Control+s');
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.vectorspace$/);
  const output = 'artifacts/downloaded-document.vectorspace'; await download.saveAs(output);
  const saved = JSON.parse(await fs.readFile(output, 'utf8'));
  expect(saved.formatVersion).toBe(1); expect(saved.pages.length).toBe(3);
  const beforeReload = await state(page);
  await page.waitForFunction(async () => {
    const stored = await globalThis.vectorSpaceStorage.load(); return !!stored;
  });
  await page.waitForTimeout(1300);
  await page.reload(); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready);
  expect((await state(page)).nodes).toBe(beforeReload.nodes);
  expect(errors).toEqual([]);
});

test('zoom anchoring, tools and narrower viewport remain interactive', async ({ page }) => {
  await ready(page);
  await page.mouse.click(650, 240);
  const initial = await state(page);
  await page.keyboard.down('Control'); await page.mouse.wheel(0, -240); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).zoom).not.toBe(initial.zoom);
  await page.keyboard.press('Shift+1');
  await page.keyboard.press('o');
  await expect.poll(async () => (await state(page)).tool).toBe('Ellipse');
  await page.mouse.move(660, 300); await page.mouse.down(); await page.mouse.move(780, 400, { steps: 10 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).kind).toBe('Ellipse');
  await page.keyboard.press('Escape');
  await page.keyboard.press('p');
  await expect.poll(async () => (await state(page)).tool).toBe('Pen');
  await page.mouse.click(600, 540); await page.mouse.click(700, 540); await page.mouse.click(650, 630); await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).kind).toBe('Path');
  await page.setViewportSize({ width: 1100, height: 760 });
  await page.waitForTimeout(500);
  await page.screenshot({ path: 'artifacts/screenshots/workbench-compact.png' });
  expect((await state(page)).canvasWidth).toBeGreaterThan(300);
});
