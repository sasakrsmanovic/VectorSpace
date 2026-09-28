import { test, expect, state, shared, click, input, screen, point, select, synced, room, invite, join, document, downloadCopy, node } from './collaboration-helpers.mjs';
import { pixels } from './png-pixels.mjs';

test('@collaboration two editors synchronize presence and preserve peer edits during own undo', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
  await expect.poll(async () => (await shared(page)).names).toContain('Bob');
  await expect.poll(async () => (await shared(bob)).names).toContain('Alice');
  await select(page, 'a'); await page.keyboard.press('ArrowRight'); await synced(page, 1); await synced(bob, 1);
  await select(bob, 'a'); await bob.keyboard.press('ArrowDown'); await synced(bob, 2); await synced(page, 2);
  await expect.poll(async () => (await state(page)).y).toBe(81);
  await page.keyboard.press('Control+z'); await synced(page, 3); await synced(bob, 3);
  expect((await state(bob)).x).toBe(80); expect((await state(bob)).y).toBe(81);
  await page.keyboard.press('Control+Shift+z'); await synced(page, 4); await synced(bob, 4);
  expect((await state(bob)).x).toBe(81); expect((await state(bob)).y).toBe(81);
  const p = await screen(bob, 600, 400); await bob.mouse.move(p.x, p.y); await page.waitForTimeout(400);
  const image = pixels(await page.screenshot({ path: 'artifacts/screenshots/collaboration-presence.png' }));
  const red = await screen(page, 100, 100); expect(image(red.x, red.y)[0]).toBeGreaterThan(200);
  expect(node(await document(page, 'shared-undo.vectorspace'), 'a').y).toBe(81);
});

test('@collaboration remote updates wait for the active pointer gesture and preserve both results', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob'); await select(page, 'a');
  const a = await screen(page, 160, 130), b = await screen(page, 200, 150);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 6 });
  await expect.poll(async () => (await state(page)).interacting).toBe(true);
  await select(bob, 'b'); await bob.keyboard.press('ArrowDown'); await synced(bob, 1);
  await page.waitForTimeout(300); expect((await shared(page)).revision).toBe(0);
  await page.mouse.up(); await synced(page, 2); await synced(bob, 2);
  const saved = await document(page, 'shared-gesture.vectorspace'); expect(node(saved, 'a').x).toBeGreaterThan(80); expect(node(saved, 'b').y).toBe(81);
  await page.keyboard.press('Control+z'); await synced(page, 3); await synced(bob, 3);
  const undone = await document(page, 'shared-gesture-undo.vectorspace'); expect(node(undone, 'a').x).toBe(80); expect(node(undone, 'b').y).toBe(81);
});

test('@collaboration viewer input cannot mutate the design and commenter input can add synchronized comments', async ({ page, peer, request }) => {
  const owner = await room(request), viewer = await invite(request, owner, 'Viewer', 'Viewer'), commenter = await invite(request, owner, 'Reviewer', 'Commenter');
  const bob = await peer(), review = await peer(); await join(page, owner, 'Alice'); await join(bob, viewer, 'Viewer');
  await select(bob, 'a'); await bob.keyboard.press('ArrowRight'); await bob.keyboard.press('Delete');
  await bob.waitForTimeout(300); expect((await shared(bob)).revision).toBe(0); expect((await state(bob)).x).toBe(80);
  await join(review, commenter, 'Reviewer'); await point(review, 600, 420); await review.keyboard.press('c'); await point(review, 620, 430);
  await input(review, 'Add a comment', 'Review from another window'); await click(review, 'Continue');
  await synced(review, 1); await synced(page, 1);
  const saved = await document(page, 'shared-comment.vectorspace');
  expect(saved.comments.some(c => c.text === 'Review from another window' && c.author === 'Reviewer')).toBe(true);
  expect(node(saved, 'a').x).toBe(80);
});

test('@collaboration disconnected local edits reconnect without replacing a peer property', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob'); await select(page, 'a');
  await page.context().setOffline(true); await page.keyboard.press('ArrowRight');
  await expect.poll(async () => (await shared(page)).pending).toBe(1);
  await expect.poll(async () => (await shared(page)).recoverySaved).toBe(true);
  await select(bob, 'a'); await bob.keyboard.press('ArrowDown'); await synced(bob, 1);
  await page.context().setOffline(false); await synced(page, 2); await synced(bob, 2);
  await expect.poll(async () => (await state(page)).x).toBe(81); expect((await state(page)).y).toBe(81);
  expect((await shared(page)).recovery).toBe(0);
});

test('@collaboration a conflicting offline gesture is recoverable after browser reload', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob'); await select(page, 'a');
  await page.context().setOffline(true); await page.keyboard.press('ArrowRight');
  await expect.poll(async () => (await shared(page)).pending).toBe(1);
  await select(bob, 'a'); await bob.keyboard.press('Shift+ArrowRight'); await synced(bob, 1);
  await page.context().setOffline(false);
  await expect.poll(async () => (await shared(page)).recovery).toBe(1); await synced(page, 1);
  await expect.poll(async () => (await state(page)).x).toBe(90);
  await expect.poll(async () => (await shared(page)).recoverySaved).toBe(true);
  await page.reload(); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  expect((await shared(page)).connected).toBe(false);
  await click(page, 'Share'); await click(page, 'Local collaboration recovery');
  const restored = await downloadCopy(page, () => click(page, 'Download recovery Move layers'), 'shared-conflict-recovery.vectorspace');
  expect(node(restored, 'a').x).toBe(81);
});

test('@collaboration follow mode tracks a participant and local input stops following', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob'); await expect.poll(async () => (await shared(bob)).names).toContain('Alice');
  await click(bob, 'Follow Alice'); await expect.poll(async () => (await shared(bob)).following).toBe((await shared(page)).client);
  await select(page, 'a'); await page.keyboard.press('Shift+2');
  await expect.poll(async () => (await state(bob)).zoom).toBeCloseTo((await state(page)).zoom, 5);
  await expect.poll(async () => (await state(bob)).panX).toBeCloseTo((await state(page)).panX, 5);
  await bob.mouse.click(700, 500); await expect.poll(async () => (await shared(bob)).following).toBeNull();
});

test('@collaboration concurrent comment replies survive modal boundaries and local undo', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
  for (const p of [page, bob]) { await point(p, 600, 420); await p.keyboard.press('c'); await point(p, 580, 300); }
  await input(page, 'Reply', 'First response'); await input(bob, 'Reply', 'Second response');
  await click(page, 'Reply', 'Button'); await synced(page, 1);
  await click(bob, 'Reply', 'Button'); await synced(bob, 2); await synced(page, 2);
  const saved = await document(page, 'shared-replies.vectorspace');
  expect(saved.comments[0].replies).toEqual(['Alice: First response', 'Bob: Second response']);
  await point(bob, 600, 420); await bob.keyboard.press('v'); await bob.keyboard.press('Control+z'); await synced(bob, 3); await synced(page, 3);
  expect((await document(page, 'shared-replies-undo.vectorspace')).comments[0].replies).toEqual(['Alice: First response']);
});
