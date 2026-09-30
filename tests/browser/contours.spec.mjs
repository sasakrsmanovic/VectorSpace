import { test, expect } from '@playwright/test';
import { action, child, click, control, fixture, inspect, open, point, save, screen, state } from './shape-helpers.mjs';
import { pixels } from './png-pixels.mjs';

function document(path = 'M0 0H240V200H0Z M60 50V150H180V50Z M280 0H340V60H280Z') {
  return fixture([{ id: 'compound', name: 'Compound vector', kind: 'Path', x: 160, y: 180, width: 360, height: 240,
    pathWidth: 360, pathHeight: 240, pathData: path, fills: [{ color: '#0D99FF' }], strokes: [] }]);
}
async function edit(page, doc = document()) {
  await open(page, doc); await point(page, 185, 210); await expect.poll(async () => (await state(page)).id).toBe('compound');
  await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(true);
}
async function anchor(page, x, y, shift = false) {
  if (shift) await page.keyboard.down('Shift'); await point(page, x, y, false); if (shift) await page.keyboard.up('Shift');
}
async function drag(page, from, to) {
  const a = await screen(page, ...from), b = await screen(page, ...to);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 6 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).interacting).toBe(false);
}
async function color(page, x, y) {
  const p = await screen(page, x, y); return pixels(await page.screenshot())(p.x, p.y).slice(0, 3);
}

test('compound SVG converts through Enter and preserves holes, disjoint contours, placement and undo', async ({ page }) => {
  await edit(page); expect((await state(page)).contours).toBe(3); expect((await state(page)).points).toBe(12);
  expect(await color(page, 280, 280)).toEqual([255, 255, 255]); expect(await color(page, 470, 210)).toEqual([13, 153, 255]);
  const n = child(await save(page, 'compound-conversion.vectorspace'), 'compound');
  expect(n.contours).toHaveLength(3); expect(n.contours.every(c => c.closed)).toBe(true); expect(n.pathData ?? null).toBeNull();
  expect([n.x, n.y, n.width, n.height]).toEqual([160, 180, 360, 240]);
  await page.keyboard.press('Control+z'); const restored = child(await save(page, 'compound-source-restored.vectorspace'), 'compound');
  expect(restored.pathData).toBe(document().pages[0].nodes[0].children[0].pathData); expect(restored.contours ?? null).toBeNull();
});

test('anchors in different contours move together while unrelated anchors and layer coordinates stay fixed', async ({ page }) => {
  await edit(page); await anchor(page, 160, 180); await anchor(page, 220, 230, true);
  expect((await state(page)).selectedPoints).toEqual([0, 4]);
  await drag(page, [160, 180], [180, 190]); await page.keyboard.press('ArrowRight');
  const n = child(await save(page, 'compound-anchor-move.vectorspace'), 'compound');
  expect(n.contours[0].points[0].position.x).toBeCloseTo(21, 2); expect(n.contours[0].points[0].position.y).toBeCloseTo(10, 2);
  expect(n.contours[1].points[0].position.x).toBeCloseTo(81, 2); expect(n.contours[1].points[0].position.y).toBeCloseTo(60, 2);
  expect(n.contours[0].points[1].position).toEqual({ x: 240, y: 0 }); expect(n.contours[2].points[0].position).toEqual({ x: 280, y: 0 });
  expect([n.x, n.y]).toEqual([160, 180]);
  await page.screenshot({ path: 'artifacts/screenshots/compound-anchor-editing.png' });
});

test('custom contour navigation and deletion remove only the hole contour and undo restores it', async ({ page }) => {
  await edit(page); await inspect(page, 'Next contour'); await click(page, 'Next contour');
  expect((await state(page)).activeContour).toBe(1); expect((await state(page)).selectedPoints).toEqual([4, 5, 6, 7]);
  await inspect(page, 'Delete contour'); await click(page, 'Delete contour');
  await expect.poll(async () => (await state(page)).contours).toBe(2); expect(await color(page, 280, 280)).toEqual([13, 153, 255]);
  // The control restores canvas focus, so undo needs no focus-changing click.
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).contours).toBe(3);
  expect(await color(page, 280, 280)).toEqual([255, 255, 255]);
  const n = child(await save(page, 'compound-delete-undo.vectorspace'), 'compound'); expect(n.contours[1].points).toHaveLength(4);
});

test('nonzero and evenodd controls alter real compound fill pixels and persist the rule', async ({ page }) => {
  await edit(page, document('M0 0H240V200H0Z M60 50H180V150H60Z M280 0H340V60H280Z'));
  expect(await color(page, 280, 280)).toEqual([13, 153, 255]);
  await inspect(page, 'Even-odd'); await click(page, 'Even-odd');
  await expect.poll(async () => (await state(page)).fillRule).toBe('EvenOdd'); expect(await color(page, 280, 280)).toEqual([255, 255, 255]);
  const n = child(await save(page, 'compound-evenodd.vectorspace'), 'compound'); expect(n.fillRule).toBe('EvenOdd');
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).fillRule).toBe('NonZero');
  expect(await color(page, 280, 280)).toEqual([13, 153, 255]);
});

test('cut and join commands keep exact contour coordinates and remain undoable', async ({ page }) => {
  await edit(page); await anchor(page, 160, 180); await page.keyboard.press('x');
  const cut = child(await save(page, 'compound-cut.vectorspace'), 'compound');
  expect(cut.contours[0].closed).toBe(false); expect(cut.contours[0].points).toHaveLength(5);
  // Move the first endpoint far enough to select the coincident cut seam's
  // other endpoint independently with the normal pointer hit tolerance.
  await anchor(page, 160, 180); await page.keyboard.press('Shift+ArrowLeft');
  await anchor(page, 160, 180, true); expect((await state(page)).selectedPoints).toEqual([0, 4]);
  await page.keyboard.press('Control+j');
  const joined = child(await save(page, 'compound-joined.vectorspace'), 'compound');
  expect(joined.contours[0].closed).toBe(true); expect(joined.contours[0].points[0].position.x).toBe(-10);
  await page.keyboard.press('Control+z'); await page.keyboard.press('Control+z'); await page.keyboard.press('Control+z');
  const restored = child(await save(page, 'compound-cut-restored.vectorspace'), 'compound');
  expect(restored.contours[0].closed).toBe(true); expect(restored.contours[0].points).toHaveLength(4);
});

test('Undo and Escape during compound dragging stop subsequent stale pointer movement', async ({ page }) => {
  await edit(page); await anchor(page, 220, 230);
  const original = child(await save(page, 'compound-before-cancel.vectorspace'), 'compound');
  for (const cancel of ['Escape', 'Control+z']) {
    const p = await screen(page, 220, 230); await page.mouse.move(p.x, p.y); await page.mouse.down();
    await page.mouse.move(p.x + 40, p.y + 30, { steps: 5 }); await expect.poll(async () => (await state(page)).interacting).toBe(true);
    await page.keyboard.press(cancel); await page.mouse.move(p.x + 90, p.y + 50, { steps: 5 }); await page.mouse.up();
    expect((await state(page)).vectorEditing).toBe(true);
    const restored = child(await save(page, 'compound-cancel-' + cancel.replace('+', '') + '.vectorspace'), 'compound');
    expect(restored.contours).toEqual(original.contours); expect((await state(page)).interacting).toBe(false);
  }
});

test('subdivision uses the active contour seam rather than bridging into its neighbor', async ({ page }) => {
  await edit(page); await inspect(page, 'Next contour'); await click(page, 'Next contour');
  await inspect(page, 'Split selected segments'); await click(page, 'Split selected segments');
  await expect.poll(async () => (await state(page)).points).toBe(16);
  const n = child(await save(page, 'compound-subdivide.vectorspace'), 'compound');
  expect(n.contours.map(c => c.points.length)).toEqual([4, 8, 4]); expect(n.contours[1].points[7].position).toEqual({ x: 120, y: 50 });
  expect(await color(page, 280, 280)).toEqual([255, 255, 255]);
});
