import { test, expect, shared, select, synced, room, invite, join, document, node } from './collaboration-helpers.mjs';
import { field, inspect, click, point, state, action } from './shape-helpers.mjs';

// Rooms and invitations are test setup only. The shared edits themselves are made
// through the actual Uno controls in independent browser contexts.
test('@collaboration independent corners and stroke geometry converge without overwriting a peer move', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob'); await select(page, 'a');
  await inspect(page, 'Independent'); await click(page, 'Independent');
  await field(page, 'Top L', 36); const revision = (await shared(page)).revision;
  await expect.poll(async () => (await shared(page)).pending).toBe(0);
  await expect.poll(async () => (await shared(bob)).revision).toBe((await shared(page)).revision);
  await select(bob, 'a'); await bob.keyboard.press('ArrowRight');
  await expect.poll(async () => (await shared(bob)).pending).toBe(0);
  await expect.poll(async () => (await state(page)).x).toBe(81);
  // The latest own entry changes only corners. Its inverse must retain Bob's X edit.
  await point(page, 150, 130); await page.keyboard.press('Control+z');
  await expect.poll(async () => (await shared(page)).pending).toBe(0);
  await expect.poll(async () => (await state(bob)).corners?.[0]).toBe(0);
  expect((await state(bob)).x).toBe(81);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(bob)).corners?.[0]).toBe(36);
  // Outline a stroke using the existing stroke section's add command, then
  // confirm the new native contour representation crosses the shared wire.
  await inspect(page, 'Stroke options'); await click(page, 'Stroke options');
  await inspect(page, 'Outside'); await click(page, 'Outside');
  await point(page, 150, 130); await action(page, 'Outline stroke');
  await expect.poll(async () => (await state(bob)).kind).toBe('Group');
  await expect.poll(async () => (await shared(page)).pending).toBe(0);
  await expect.poll(async () => (await shared(bob)).revision).toBe((await shared(page)).revision);
  const saved = await document(bob, 'shared-shape-outline.vectorspace');
  expect(saved.formatVersion).toBe(6); expect(node(saved, 'a').children.some(c => c.commands?.length > 0)).toBe(true);
  expect(node(saved, 'a').x).toBe(81);
  expect((await shared(page)).recovery).toBe(0); expect((await shared(bob)).recovery).toBe(0);
});
