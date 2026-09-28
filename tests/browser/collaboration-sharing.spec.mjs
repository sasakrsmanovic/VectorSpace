import { test, expect, state, shared, control, click, input, point, select, synced, room, invite, join, downloadCopy, node, server } from './collaboration-helpers.mjs';

test('@collaboration owner creates a room and guest invitation through the real Share controls', async ({ page, peer }) => {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await click(page, 'Share');
  await input(page, 'Collaboration display name', 'Alice');
  await input(page, 'Collaboration server', server());
  await input(page, 'Collaboration creation key', process.env.VECTORSPACE_CREATE_KEY, 'PasswordBox');
  await click(page, 'Create shared file'); await click(page, 'Continue');
  await expect.poll(async () => (await shared(page)).connected).toBe(true);
  await control(page, 'Private collaboration link');
  const controls = await page.evaluate(() => globalThis.__vectorSpaceControls);
  expect(controls.filter(c => c.name === 'Private collaboration link').every(c => !('value' in c))).toBe(true);
  await click(page, 'Close');
  await click(page, 'Share'); await click(page, 'Create invitation'); await input(page, 'Invitation label', 'Bob');
  await click(page, 'Create link'); await click(page, 'Copy link');
  const link = await page.evaluate(() => navigator.clipboard.readText()); expect(link).toContain('#collaboration=');
  const bob = await peer(); await bob.goto(link);
  await bob.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(bob, 'Collaboration invitation link');
  expect((await shared(bob)).connected).toBe(false); expect(bob.url()).not.toContain('#collaboration=');
  await input(bob, 'Collaboration display name', 'Bob'); await click(bob, 'Join file'); await click(bob, 'Continue');
  await expect.poll(async () => (await shared(bob)).connected).toBe(true);
  await expect.poll(async () => (await shared(bob)).role).toBe('Editor');
  await expect.poll(async () => (await shared(page)).names).toContain('Bob');
  await click(page, 'Share'); await page.screenshot({ path: 'artifacts/screenshots/collaboration-share.png' }); await click(page, 'Close');
});

test('@collaboration history restores as a new revision and revocation removes active access', async ({ page, peer, request }) => {
  const owner = await room(request), guest = await invite(request, owner), bob = await peer();
  await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
  await select(page, 'a'); await page.keyboard.press('ArrowRight'); await synced(page, 1);
  await page.keyboard.press('ArrowRight'); await synced(page, 2); await synced(bob, 2);
  await click(page, 'Share'); await click(page, 'Version history');
  const old = await downloadCopy(page, () => click(page, 'Download revision 1'), 'shared-history-r1.vectorspace');
  expect(node(old, 'a').x).toBe(81);
  await click(page, 'Share'); await click(page, 'Version history');
  await page.screenshot({ path: 'artifacts/screenshots/collaboration-history.png' });
  await click(page, 'Restore revision 1'); await click(page, 'Continue');
  await synced(page, 3); await synced(bob, 3); await select(bob, 'a'); expect((await state(bob)).x).toBe(81);
  await click(page, 'Share'); await click(page, 'Manage invitations'); await click(page, 'Revoke Bob'); await click(page, 'Continue');
  await expect.poll(async () => (await shared(bob)).denied).toBe(true);
  await bob.keyboard.press('ArrowRight'); await bob.waitForTimeout(300); expect((await state(bob)).x).toBe(81);
});
