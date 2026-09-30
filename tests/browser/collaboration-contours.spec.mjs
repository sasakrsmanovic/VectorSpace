import { test, expect, fixture, room, invite, join, shared, node } from './collaboration-helpers.mjs';
import { point, save, state } from './shape-helpers.mjs';

test('@collaboration compound anchor edits synchronize and own undo preserves a peer transform', async ({ page, peer, request }) => {
  const doc = fixture(); doc.formatVersion = 7;
  const a = doc.pages[0].nodes[0].children[0]; a.kind = 'Path'; a.pathWidth = 160; a.pathHeight = 100;
  a.contours = [ [[0, 0], [160, 0], [160, 100], [0, 100]], [[40, 25], [40, 75], [120, 75], [120, 25]] ].map(loop => ({ closed: true, points: loop.map(([x, y]) => ({ position: { x, y } })) }));
  const owner = await room(request, doc), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
  await point(page, 100, 95); await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(true);
  await point(page, 120, 105, false); await page.keyboard.press('Shift+ArrowRight');
  await expect.poll(async () => (await shared(page)).pending).toBe(0);
  const ownRevision = (await shared(page)).revision;
  await expect.poll(async () => (await shared(bob)).revision).toBe(ownRevision);
  await point(bob, 100, 95); await pageKeyboard(bob, 'ArrowRight');
  await expect.poll(async () => (await shared(bob)).pending).toBe(0);
  await expect.poll(async () => (await state(page)).x).toBe(81);
  // Point-edit history is conditional on the compound property only, not X.
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await shared(page)).pending).toBe(0);
  await expect.poll(async () => (await shared(bob)).revision).toBe((await shared(page)).revision);
  const undone = node(await save(bob, 'compound-shared-undo.vectorspace'), 'a');
  expect(undone.x).toBe(81); expect(undone.contours[1].points[0].position).toEqual({ x: 40, y: 25 });
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await shared(page)).pending).toBe(0);
  await expect.poll(async () => (await shared(bob)).revision).toBe((await shared(page)).revision);
  const redone = node(await save(bob, 'compound-shared-redo.vectorspace'), 'a');
  expect(redone.x).toBe(81); expect(redone.contours[1].points[0].position).toEqual({ x: 50, y: 25 });
  expect((await shared(page)).recovery).toBe(0); expect((await shared(bob)).recovery).toBe(0);
});
async function pageKeyboard(page, key) { await page.keyboard.press(key); }
