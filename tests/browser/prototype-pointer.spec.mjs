import { test, expect } from '@playwright/test';

const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
async function control(page, name) {
  await expect.poll(() => page.evaluate(name => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && c.enabled), name)).toBe(true);
  return page.evaluate(name => globalThis.__vectorSpaceControls.filter(c => c.name === name && c.enabled).at(-1), name);
}
async function click(page, name) { const c = await control(page, name); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
function fixture() {
  const rect = (id, x, y, width, height, reactions = []) => ({ id, name: id, kind: 'Rectangle', x, y, width, height, reactions, fills: [{ color: '#0D99FF' }] });
  const hover = rect('hover', 20, 20, 140, 60, [
    { trigger: 'MouseEnter', actions: [{ kind: 'SetVariable', variableId: 'count', operation: 'Add', value: { type: 'Number', number: 1 } }, { kind: 'SetVariable', variableId: 'flag', value: { type: 'Boolean', boolean: true } }] },
    { trigger: 'MouseLeave', actions: [{ kind: 'SetVariable', variableId: 'flag', value: { type: 'Boolean', boolean: false } }] }
  ]);
  hover.kind = 'Frame'; hover.children = [rect('left', 0, 0, 70, 60), rect('right', 70, 0, 70, 60)];
  const a = { id: 'a', kind: 'Frame', name: 'Pointer test', width: 400, height: 300, prototypeFlowName: 'Pointer flow', children: [hover,
    rect('click', 20, 120, 100, 50, [{ actions: [{ kind: 'Navigate', targetId: 'b' }] }]),
    rect('press', 220, 120, 100, 50, [{ trigger: 'MouseDown', actions: [{ kind: 'Navigate', targetId: 'b' }] }])
  ] };
  const b = { id: 'b', kind: 'Frame', name: 'Destination', x: 600, width: 400, height: 300, children: [
    rect('release', 220, 120, 100, 50, [{ trigger: 'MouseUp', actions: [{ kind: 'Back' }] }, { actions: [{ kind: 'Back' }] }])
  ] };
  return { formatVersion: 3, pages: [{ id: 'pointer-page', name: 'Pointer tests', nodes: [a, b] }],
    variableCollections: [{ id: 'collection', defaultModeId: 'default', modes: [{ id: 'default' }] }],
    variables: [{ id: 'flag', collectionId: 'collection', type: 'Boolean', values: { default: { type: 'Boolean' } } },
      { id: 'count', collectionId: 'collection', type: 'Number', values: { default: { type: 'Number' } } }]
  };
}
async function open(page, document = fixture()) {
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'pointer.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(document)) });
  await expect.poll(async () => (await state(page)).page).toBe('Pointer tests');
  const c = await control(page, 'Design canvas'); const s = await state(page);
  await page.keyboard.down('Control'); await page.mouse.click(c.x + s.panX + 350 * s.zoom, c.y + s.panY + 270 * s.zoom); await page.keyboard.up('Control');
  await expect.poll(async () => (await state(page)).id).toBe('a');
  await click(page, 'Present prototype'); await expect.poll(async () => (await state(page)).presenting).toBe(true); await control(page, 'Prototype canvas');
}
async function point(page, x, y) {
  const c = await control(page, 'Prototype canvas'); const p = (await state(page)).prototype;
  return { x: c.x + p.panX + x * p.zoom, y: c.y + p.panY + y * p.zoom };
}
async function move(page, x, y) { const p = await point(page, x, y); await page.mouse.move(p.x, p.y); }

test('hover enters once across sibling hit targets and leaves the reactive owner', async ({ page }) => {
  await open(page); await move(page, 350, 270); await move(page, 50, 45);
  await expect.poll(async () => (await state(page)).prototype.values.count).toBe('1');
  expect((await state(page)).prototype.values.flag).toBe('True');
  await move(page, 125, 45); await page.waitForTimeout(200);
  expect((await state(page)).prototype.values.count).toBe('1'); expect((await state(page)).prototype.values.flag).toBe('True');
  await move(page, 350, 270); await expect.poll(async () => (await state(page)).prototype.values.flag).toBe('False');
  await move(page, 50, 45); await expect.poll(async () => (await state(page)).prototype.values.count).toBe('2');
  await click(page, 'Prototype restart'); await expect.poll(async () => (await state(page)).prototype.values.count).toBe('0');
});

test('pointer drags and release outside the pressed owner cancel click activation', async ({ page }) => {
  await open(page); await move(page, 60, 145); await page.mouse.down(); await move(page, 95, 145); await page.mouse.up();
  await page.waitForTimeout(200); expect((await state(page)).prototype.frame).toBe('a');
  await move(page, 60, 145); await page.mouse.down(); await move(page, 350, 270); await page.mouse.up();
  await page.waitForTimeout(200); expect((await state(page)).prototype.frame).toBe('a');
  const p = await point(page, 60, 145); await page.mouse.click(p.x, p.y); await expect.poll(async () => (await state(page)).prototype.frame).toBe('b');
});

test('a press navigation cannot release-activate a target in the newly entered frame', async ({ page }) => {
  await open(page); await move(page, 260, 145); await page.mouse.down();
  await expect.poll(async () => (await state(page)).prototype.frame).toBe('b');
  await page.mouse.up(); await page.waitForTimeout(200); expect((await state(page)).prototype.frame).toBe('b');
  const p = await point(page, 260, 145); await page.mouse.click(p.x, p.y); await expect.poll(async () => (await state(page)).prototype.frame).toBe('a');
});

test('hover leave is reconciled when an animation finishes without another mouse move', async ({ page }) => {
  const document = fixture();
  document.pages[0].nodes[0].children[0].reactions[0].actions.at(-1).transition = { kind: 'Dissolve', durationMilliseconds: 800 };
  await open(page, document); await move(page, 350, 270); await move(page, 50, 45);
  await expect.poll(async () => (await state(page)).prototype.values.flag).toBe('True');
  expect((await state(page)).prototype.animating).toBe(true);
  await move(page, 350, 270);
  // No subsequent input: the player's timer must reconcile the retained pointer location.
  await expect.poll(async () => (await state(page)).prototype.values.flag).toBe('False');
  expect((await state(page)).prototype.values.count).toBe('1');
  await expect.poll(async () => (await state(page)).prototype.animating).toBe(false);
});
