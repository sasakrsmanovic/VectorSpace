import { test, expect } from '@playwright/test';
const state = page => page.evaluate(() => globalThis.__vectorSpaceState);

test('resize handles and cancelling an unfinished pen path remain undoable', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('?test=1');
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150_000 });
  await page.waitForTimeout(1000);
  await page.mouse.click(650, 240);
  const initial = await state(page);
  await page.keyboard.press('r');
  await page.mouse.move(650, 300); await page.mouse.down();
  await page.mouse.move(810, 420, { steps: 10 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).kind).toBe('Rectangle');
  const beforeResize = await state(page);
  await page.mouse.move(810, 420); await page.mouse.down();
  await page.mouse.move(870, 460, { steps: 12 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).width).toBeGreaterThan(beforeResize.width + 40);
  expect((await state(page)).history).toBe(beforeResize.history + 1);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).width).toBeCloseTo(beforeResize.width, 3);
  await page.keyboard.press('Escape');
  await page.keyboard.press('p');
  await page.mouse.click(600, 540); await page.mouse.click(700, 540);
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 2);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 1);
  // A new pen gesture must use current document nodes, not the cancelled snapshot.
  await page.mouse.click(600, 550); await page.mouse.click(700, 550);
  await page.mouse.click(650, 630); await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).kind).toBe('Path');
  expect((await state(page)).nodes).toBe(initial.nodes + 2);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).nodes).toBe(initial.nodes + 1);
  expect(errors).toEqual([]);
});
