import { test, expect } from '@playwright/test';
import { pixels } from './png-pixels.mjs';
import { state, click, input, inspect, field, choose, screen, point, action, fixture, open, save, grip, child } from './shape-helpers.mjs';

async function rectangle(page) { await point(page, 250, 240); await expect.poll(async () => (await state(page)).id).toBe('rect'); }
async function ellipse(page) { await point(page, 540, 240); await expect.poll(async () => (await state(page)).id).toBe('ellipse'); }
async function shapeEdit(page) { await inspect(page, 'Edit shape on canvas'); await click(page, 'Edit shape on canvas'); await expect.poll(async () => (await state(page)).shapeEditing).toBe(true); }

// Actual pointer/keyboard authoring; diagnostics expose coordinates, not mutations.
test('independent corner controls alter real pixels and persist exact authored radii', async ({ page }) => {
  await open(page); await rectangle(page); await inspect(page, 'Independent'); await click(page, 'Independent');
  await field(page, 'Top L', 70); await field(page, 'Top R', 0); await field(page, 'Bottom L', 0); await field(page, 'Bottom R', 35);
  await expect.poll(async () => (await state(page)).corners).toEqual([70, 0, 35, 0]);
  await page.waitForTimeout(200); const get = pixels(await page.screenshot({ path: 'artifacts/screenshots/independent-corners.png' }));
  const empty = await screen(page, 162, 172), filled = await screen(page, 337, 172);
  expect(get(empty.x, empty.y)).toEqual([255, 255, 255]); expect(get(filled.x, filled.y)[0]).toBeGreaterThan(220); expect(get(filled.x, filled.y)[2]).toBeLessThan(80);
  await point(page, 250, 240); const doc = await save(page, 'shape-corners.vectorspace'); expect(doc.formatVersion).toBe(6);
  expect(child(doc, 'rect').corners).toEqual({ topLeft: 70, topRight: 0, bottomRight: 35, bottomLeft: 0 });
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).corners[2]).toBe(12);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).corners[2]).toBe(35);
});

test('corner grips support Alt independence and Undo cancels capture before further pointer movement', async ({ page }) => {
  await open(page); await rectangle(page); await shapeEdit(page);
  let h = await grip(page, 0);
  await page.keyboard.down('Alt'); await page.mouse.move(h.x, h.y); await page.mouse.down();
  await page.mouse.move(h.x + 24 * h.zoom, h.y + 24 * h.zoom, { steps: 8 }); await page.mouse.up(); await page.keyboard.up('Alt');
  await expect.poll(async () => (await state(page)).corners?.[0]).toBeCloseTo(36, 1);
  expect((await state(page)).corners.slice(1)).toEqual([12, 12, 12]);
  // Pointer coordinates cross browser/Uno floating-point boundaries. The committed
  // baseline, not a rounded literal, must be restored bit-for-bit by cancellation.
  const committedCorners = (await state(page)).corners;
  h = await grip(page, 0); await page.mouse.move(h.x, h.y); await page.mouse.down(); await page.mouse.move(h.x + 10 * h.zoom, h.y + 10 * h.zoom);
  await page.keyboard.press('Control+z');
  await page.mouse.move(h.x + 40 * h.zoom, h.y + 40 * h.zoom); await page.mouse.up();
  await expect.poll(async () => (await state(page)).shapeEditing).toBe(false);
  expect((await state(page)).corners).toEqual(committedCorners); expect((await state(page)).x).toBe(150);
  await shapeEdit(page); h = await grip(page, 2); await page.mouse.move(h.x, h.y); await page.mouse.down();
  await page.mouse.move(h.x - 16 * h.zoom, h.y - 16 * h.zoom, { steps: 5 }); await page.keyboard.press('Escape'); await page.mouse.up();
  await expect.poll(async () => (await state(page)).shapeEditing).toBe(false);
  expect((await state(page)).corners).toEqual(committedCorners);
});

test('ellipse arc inspector creates a ring hole and edits signed sweep without losing shape identity', async ({ page }) => {
  await open(page); await ellipse(page); await inspect(page, 'Ring'); await click(page, 'Ring');
  await field(page, 'Inner %', 60); await field(page, 'Start', -40); await field(page, 'Sweep', 280);
  await expect.poll(async () => (await state(page)).arcInner).toBe(.6);
  await expect.poll(async () => (await state(page)).arcSweep).toBe(280);
  await page.waitForTimeout(200); const get = pixels(await page.screenshot({ path: 'artifacts/screenshots/ellipse-ring-controls.png' }));
  const center = await screen(page, 540, 240), ring = await screen(page, 601, 240);
  expect(get(center.x, center.y)).toEqual([255, 255, 255]); expect(get(ring.x, ring.y)[1]).toBeGreaterThan(140);
  await point(page, 601, 240); const doc = await save(page, 'ellipse-ring.vectorspace');
  expect(child(doc, 'ellipse').kind).toBe('Ellipse'); expect(child(doc, 'ellipse').arc).toEqual({ startDegrees: -40, sweepDegrees: 280, innerRadius: .6, open: false });
  await field(page, 'Sweep', -160); await expect.poll(async () => (await state(page)).arcSweep).toBe(-160);
});

test('on-canvas ellipse grips change inner radius and constrain sweep with undoable transactions', async ({ page }) => {
  const doc = fixture(); doc.pages[0].nodes[0].children[1].arc = { startDegrees: 0, sweepDegrees: 180, innerRadius: .25 };
  await open(page, doc); await point(page, 600, 270); await expect.poll(async () => (await state(page)).id).toBe('ellipse'); await shapeEdit(page);
  let h = await grip(page, 2), target = await screen(page, 540, 280);
  await page.mouse.move(h.x, h.y); await page.mouse.down(); await page.mouse.move(target.x, target.y, { steps: 8 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).arcInner).toBeCloseTo(.5, 2);
  h = await grip(page, 1); target = await screen(page, 540, 160);
  await page.keyboard.down('Shift'); await page.mouse.move(h.x, h.y); await page.mouse.down(); await page.mouse.move(target.x, target.y, { steps: 10 }); await page.mouse.up(); await page.keyboard.up('Shift');
  await expect.poll(async () => (await state(page)).arcSweep).toBeCloseTo(270, 1);
  await page.screenshot({ path: 'artifacts/screenshots/ellipse-arc-grips.png' });
  await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).shapeEditing).toBe(false);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).arcSweep).toBe(180);
  expect((await state(page)).arcInner).toBeCloseTo(.5, 2);
});

test('stroke geometry controls affect outside pixels and survive style transfer', async ({ page }) => {
  await open(page); await rectangle(page); await inspect(page, 'Outside'); await click(page, 'Outside');
  await page.waitForTimeout(200); const image = pixels(await page.screenshot({ path: 'artifacts/screenshots/stroke-alignment.png' }));
  const outside = await screen(page, 141, 240), inside = await screen(page, 157, 240);
  expect(image(outside.x, outside.y)[2]).toBeGreaterThan(230); expect(image(outside.x, outside.y)[0]).toBeLessThan(20);
  expect(image(inside.x, inside.y)[0]).toBeGreaterThan(220); expect(image(inside.x, inside.y)[2]).toBeLessThan(80);
  await choose(page, 'Stroke cap', 'Square'); await choose(page, 'Stroke join', 'Bevel'); await field(page, 'Phase', 3);
  await inspect(page, 'Dash pattern'); await input(page, 'Dash pattern', '8, 6, 4'); await page.keyboard.press('Enter');
  await point(page, 250, 240); await page.keyboard.press('Control+Alt+c');
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toMatch(/^VectorSpace\.Properties\/1\n/);
  const packet = JSON.parse((await page.evaluate(() => navigator.clipboard.readText())).split('\n').slice(1).join('\n'));
  const expected = { alignment: 'Outside', cap: 'Square', join: 'Bevel', dashOffset: 3, dashes: [8, 6, 4] };
  expect(packet.properties.strokes[0]).toMatchObject(expected);
  await ellipse(page); await page.keyboard.press('Control+Alt+v');
  await expect.poll(async () => (await state(page)).fill).toBe('#F24822');
  const doc = await save(page, 'shape-stroke-transfer.vectorspace');
  expect(child(doc, 'ellipse').strokes[0]).toMatchObject(expected);
});

test('live Boolean authoring preserves editable operands and exact flatten undo', async ({ page }) => {
  const doc = fixture([
    { id: 'rect', name: 'Base', x: 150, y: 160, width: 200, height: 160, fills: [{ color: '#F24822' }] },
    { id: 'ellipse', kind: 'Ellipse', name: 'Cutout', x: 250, y: 160, width: 160, height: 160, fills: [{ color: '#14AE5C' }] }
  ]);
  await open(page, doc); await point(page, 200, 240);
  // The cutout extends beyond the selected base. Avoid its right-middle resize
  // grip at (350,240): that is a resize target, not a layer-selection target.
  await page.keyboard.down('Shift'); await point(page, 375, 240); await page.keyboard.up('Shift');
  await expect.poll(async () => (await state(page)).selection).toBe(2);
  await action(page, 'Subtract shapes (live)'); await expect.poll(async () => (await state(page)).booleanOperation).toBe('Subtract');
  const id = (await state(page)).id;
  await page.waitForTimeout(200); const get = pixels(await page.screenshot({ path: 'artifacts/screenshots/live-boolean-inspector.png' }));
  const cutout = await screen(page, 300, 240); expect(get(cutout.x, cutout.y)).toEqual([255, 255, 255]);
  await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).id).toBe('ellipse'); await page.keyboard.press('ArrowRight');
  await page.keyboard.press('Shift+Enter'); await expect.poll(async () => (await state(page)).id).toBe(id);
  const native = await save(page, 'live-boolean.vectorspace'); const group = child(native, id);
  expect(group.children.map(n => n.id)).toEqual(['rect', 'ellipse']); expect(group.children[1].x).toBe(101);
  await action(page, 'Flatten Boolean result'); await expect.poll(async () => (await state(page)).nativeCommands).toBeGreaterThan(0);
  const flattened = await save(page, 'flattened-boolean.vectorspace'); expect(child(flattened, id).commands.some(c => c.verb === 'Conic')).toBe(true);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).booleanOperation).toBe('Subtract');
  await action(page, 'Release Boolean operands'); const released = await save(page, 'released-boolean.vectorspace');
  expect(child(released, 'rect')).toBeTruthy(); expect(child(released, 'ellipse').x).toBe(251); expect(child(released, id)).toBeUndefined();
});

test('stroke outlining retains visible appearance and native contour children', async ({ page }) => {
  await open(page); await rectangle(page); await inspect(page, 'Outside'); await click(page, 'Outside');
  await point(page, 250, 240); await action(page, 'Outline stroke');
  await expect.poll(async () => (await state(page)).kind).toBe('Group'); const id = (await state(page)).id;
  const doc = await save(page, 'outlined-stroke.vectorspace'); const group = child(doc, id);
  expect(group.children).toHaveLength(2); expect(group.children.every(c => c.commands.length > 0)).toBe(true); expect(group.strokes).toHaveLength(0);
  const get = pixels(await page.screenshot({ path: 'artifacts/screenshots/outlined-stroke.png' })); const edge = await screen(page, 141, 240), body = await screen(page, 250, 240);
  expect(get(edge.x, edge.y)[2]).toBeGreaterThan(230); expect(get(body.x, body.y)[0]).toBeGreaterThan(220);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).kind).toBe('Rectangle');
});

test('shape playground opens from real quick actions and renders all original studies', async ({ page }) => {
  await open(page); await action(page, 'Shape playground'); await click(page, 'Continue');
  await expect.poll(async () => (await state(page)).page).toBe('Shape studies'); await page.waitForTimeout(400);
  await page.screenshot({ path: 'artifacts/screenshots/shape-playground.png' });
  const doc = await save(page, 'shape-playground.vectorspace'); const studies = doc.pages[0].nodes[0].children;
  expect(studies.some(n => n.arc)).toBe(true); expect(studies.some(n => n.corners)).toBe(true); expect(studies.some(n => n.boolean === 'Subtract')).toBe(true);
});
