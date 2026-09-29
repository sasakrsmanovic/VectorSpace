import { test, expect, state, shared, room, invite, join, select, synced, document, node } from './collaboration-helpers.mjs';

test('@collaboration property paste converges while own undo preserves peer geometry', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
  await select(page, 'a'); await page.keyboard.press('Control+Alt+c');
  await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toMatch(/^VectorSpace\.Properties\/1\n/);
  await select(page, 'b'); await page.keyboard.press('Control+Alt+v'); await synced(page, 1); await synced(bob, 1);
  await select(bob, 'b'); await expect.poll(async () => (await state(bob)).fill).toBe('#F24822');
  await bob.keyboard.press('ArrowDown'); await synced(bob, 2); await synced(page, 2);
  await page.keyboard.press('Control+z'); await synced(page, 3); await synced(bob, 3);
  await expect.poll(async () => (await state(bob)).fill).toBe('#0D99FF'); expect((await state(bob)).y).toBe(81);
  await page.keyboard.press('Control+Shift+z'); await synced(page, 4); await synced(bob, 4);
  const saved = await document(bob, 'shared-property-transfer.vectorspace');
  expect(saved.formatVersion).toBe(5); expect(node(saved, 'b').fills[0].color).toBe('#F24822'); expect(node(saved, 'b').y).toBe(81);
  expect((await shared(page)).recovery).toBe(0); expect((await shared(bob)).recovery).toBe(0);
});
