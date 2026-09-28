import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function control(page, name) {
  await expect.poll(() => page.evaluate(name => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && c.enabled), name)).toBe(true);
  return page.evaluate(name => globalThis.__vectorSpaceControls.filter(c => c.name === name && c.enabled).at(-1), name);
}
async function clickControl(page, name) { const c = await control(page, name); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
async function screen(page, x, y) { const c = await control(page, 'Design canvas'); const s = await state(page); return [c.x + s.panX + x * s.zoom, c.y + s.panY + y * s.zoom]; }
async function point(page, x, y) { await page.mouse.click(...await screen(page, x, y)); }
async function drag(page, a, b) { await page.mouse.move(...await screen(page, ...a)); await page.mouse.down(); await page.mouse.move(...await screen(page, ...b), { steps: 8 }); await page.mouse.up(); }
async function action(page, name) {
  await page.keyboard.press('Control+k'); await clickControl(page, 'Search quick actions'); await page.keyboard.insertText(name); await clickControl(page, name);
}
async function open(page) {
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'tools.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({
    formatVersion: 4, id: 'tools', name: 'Editing acceptance', pages: [{ id: 'tools-page', name: 'Tools', nodes: [{
      id: 'stage', kind: 'Frame', width: 800, height: 600, fills: [{ color: '#FFFFFF' }], children: [{
        id: 'target', name: 'Editable shape', x: 200, y: 170, width: 240, height: 240, fills: [{ color: '#7B61FF' }]
      }]
    }] }]
  })) });
  await expect.poll(async () => (await state(page)).page).toBe('Tools');
  await page.waitForTimeout(300);
}
async function enterPoints(page) {
  await page.keyboard.down('Control'); await point(page, 300, 270); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).id).toBe('target'); await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).vectorEditing).toBe(true);
}
async function save(page, name) {
  const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const d = await pending;
  await fs.mkdir('artifacts', { recursive: true }); const path = `artifacts/${name}.vectorspace`; await d.saveAs(path);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
const target = doc => doc.pages[0].nodes[0].children.find(n => n.id === 'target');

test('vector multi-selection moves anchors and nudges without moving the layer', async ({ page }) => {
  await open(page); await enterPoints(page); await expect.poll(async () => (await state(page)).points).toBe(4);
  await point(page, 200, 170); await page.keyboard.down('Shift'); await point(page, 440, 170); await page.keyboard.up('Shift');
  await expect.poll(async () => (await state(page)).selectedPoints.length).toBe(2);
  await drag(page, [200, 170], [220, 180]); await page.keyboard.press('ArrowRight'); await page.keyboard.press('Shift+ArrowDown');
  const edited = target(await save(page, 'multi-anchor'));
  expect(edited.points[0].position.x).toBeCloseTo(21, 2); expect(edited.points[0].position.y).toBeCloseTo(20, 2);
  expect(edited.points[1].position.x).toBeCloseTo(261, 2); expect(edited.points[2].position.x).toBe(240);
  expect(edited.x).toBe(200); expect(edited.y).toBe(170);
  await page.screenshot({ path: 'artifacts/screenshots/vector-multiselect.png' });
  await page.keyboard.press('Control+z'); const undone = target(await save(page, 'multi-anchor-undo')); expect(undone.points[0].position.y).toBeCloseTo(10, 2);
});

test('segment insertion, subdivision, tangent modes and point deletion are undoable', async ({ page }) => {
  await open(page); await enterPoints(page); await point(page, 320, 170);
  await expect.poll(async () => (await state(page)).points).toBe(5);
  await page.keyboard.press('Control+a'); await clickControl(page, 'Split selected segments');
  await expect.poll(async () => (await state(page)).points).toBe(10);
  await page.keyboard.press('Control+a'); await page.keyboard.press('b');
  const smooth = target(await save(page, 'smooth-anchors')); expect(smooth.points.every(p => p.controlIn && p.controlOut)).toBe(true);
  await page.screenshot({ path: 'artifacts/screenshots/vector-tangents.png' });
  await page.keyboard.press('Alt+b'); const corners = target(await save(page, 'corner-anchors')); expect(corners.points.every(p => !p.controlIn && !p.controlOut)).toBe(true);
  await point(page, 200, 170); await page.keyboard.press('Delete'); await expect.poll(async () => (await state(page)).points).toBe(9);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).points).toBe(10);
  await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(false);
  expect((await state(page)).nodes).toBe(2);
});

test('point marquee and Escape restore an in-flight drag without deleting the object', async ({ page }) => {
  await open(page); await enterPoints(page); await drag(page, [180, 150], [460, 185]);
  await expect.poll(async () => (await state(page)).selectedPoints.length).toBe(2);
  await page.mouse.move(...await screen(page, 200, 170)); await page.mouse.down(); await page.mouse.move(...await screen(page, 260, 210), { steps: 6 });
  await page.keyboard.press('Escape'); await page.mouse.up();
  expect((await state(page)).vectorEditing).toBe(true);
  const restored = target(await save(page, 'cancel-points')); expect(restored.points[0].position).toEqual({ x: 0, y: 0 });
  await page.keyboard.press('Escape'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(false);
  expect((await state(page)).id).toBe('target'); expect((await state(page)).nodes).toBe(2);
});

test('all sixteen palette tools are reachable through actual quick actions', async ({ page }) => {
  await open(page);
  for (const tool of ['Move', 'Scale', 'Frame', 'Section', 'Rectangle', 'Ellipse', 'Line', 'Arrow', 'Polygon', 'Star', 'Pen', 'Pencil', 'Text', 'Hand', 'Comment', 'Slice']) {
    await action(page, `Tool: ${tool}`); await expect.poll(async () => (await state(page)).tool).toBe(tool);
  }
});

test('shape tools create, adjust polygon count and retain exact horizontal lines', async ({ page }) => {
  await open(page);
  for (const tool of ['Rectangle', 'Ellipse', 'Frame', 'Section', 'Line', 'Arrow', 'Polygon', 'Star', 'Slice']) {
    await action(page, `Tool: ${tool}`);
    await page.mouse.move(...await screen(page, 510, 430)); await page.mouse.down();
    await page.mouse.move(...await screen(page, 620, tool === 'Line' ? 435 : 510), { steps: 5 });
    if (tool === 'Polygon' || tool === 'Star') await page.keyboard.press('ArrowUp');
    if (tool === 'Line') { await page.keyboard.down('Shift'); await page.mouse.move(...await screen(page, 620, 435)); }
    await page.mouse.up(); if (tool === 'Line') await page.keyboard.up('Shift');
    await expect.poll(async () => (await state(page)).kind).toBe(tool);
    if (tool === 'Polygon' || tool === 'Star') expect((await state(page)).sides).toBe(6);
    if (tool === 'Line') {
      const d = await save(page, 'horizontal-line'); const line = d.pages[0].nodes[0].children.find(n => n.kind === 'Line'); expect(line.pathData).toMatch(/^M0 0 L[\d.]+ 0$/);
    }
    await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).nodes).toBe(2);
  }
});

test('pen Back removes one anchor, whole-stroke undo is preserved and tool switching completes the contour', async ({ page }) => {
  await open(page); await page.keyboard.press('p');
  await point(page, 70, 70); await point(page, 140, 70); await point(page, 160, 130);
  await expect.poll(async () => (await state(page)).points).toBe(3); await page.keyboard.press('Backspace'); await expect.poll(async () => (await state(page)).points).toBe(2);
  await point(page, 230, 150); await page.keyboard.press('r'); await expect.poll(async () => (await state(page)).tool).toBe('Rectangle');
  const document = await save(page, 'pen-parenting'); expect(document.pages[0].nodes[0].children.some(n => n.kind === 'Path' && n.points.length === 3)).toBe(true);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).nodes).toBe(2);
  await page.keyboard.press('p'); await point(page, 60, 70); await point(page, 150, 70); await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).nodes).toBe(2); expect((await state(page)).interacting).toBe(false);
});

test('existing guides can be dragged, dropped back into the ruler and restored with undo', async ({ page }) => {
  await open(page); await page.keyboard.press('Shift+r'); const canvas = await control(page, 'Design canvas');
  await page.mouse.move(canvas.x + 330, canvas.y + 10); await page.mouse.down(); await page.mouse.move(canvas.x + 330, canvas.y + 260, { steps: 8 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).guides).toBe(1);
  const first = (await save(page, 'guide-created')).pages[0].guides[0].position;
  await page.mouse.move(canvas.x + 500, canvas.y + 260); await page.mouse.down(); await page.mouse.move(canvas.x + 500, canvas.y + 310, { steps: 8 }); await page.mouse.up();
  const moved = (await save(page, 'guide-moved')).pages[0].guides[0].position; expect(moved).toBeGreaterThan(first);
  await page.mouse.move(canvas.x + 500, canvas.y + 310); await page.mouse.down(); await page.mouse.move(canvas.x + 500, canvas.y + 10, { steps: 8 }); await page.mouse.up();
  await expect.poll(async () => (await state(page)).guides).toBe(0); await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).guides).toBe(1);
});

test('paste-in-place and keyboard resizing operate on layers outside point mode', async ({ page }) => {
  await open(page); await page.keyboard.down('Control'); await point(page, 300, 270); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).id).toBe('target'); await page.keyboard.press('Control+c'); await page.keyboard.press('Control+Shift+v');
  await expect.poll(async () => (await state(page)).nodes).toBe(3); expect((await state(page)).x).toBe(200); expect((await state(page)).y).toBe(170);
  await page.keyboard.press('Control+Alt+ArrowRight'); await expect.poll(async () => (await state(page)).width).toBe(241);
  await page.keyboard.press('Control+Alt+Shift+ArrowDown'); await expect.poll(async () => (await state(page)).height).toBe(250);
  await page.keyboard.press('Shift+h'); const d = await save(page, 'flip-and-resize');
  const clone = d.pages[0].nodes.find(n => n.id === (d.pages[0].nodes.at(-1).id)); expect(clone.width).toBe(241); expect(clone.height).toBe(250);
});
