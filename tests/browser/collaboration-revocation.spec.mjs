import { test, expect, state, shared, control, screen, point, select, room, invite, join, node } from './collaboration-helpers.mjs';

for (const editKind of ['pointer', 'text']) {
  test(`@collaboration revoked access during an active ${editKind} edit rolls back without exiting the runtime`, async ({ page, peer, request }) => {
    const owner = await room(request), guest = await invite(request, owner), bob = await peer();
    await join(page, owner, 'Alice'); await join(bob, guest, 'Bob');
    const errors = []; bob.on('pageerror', error => errors.push(error.message));
    if (editKind === 'pointer') {
      await select(bob, 'a');
      const a = await screen(bob, 160, 130), b = await screen(bob, 200, 150);
      await bob.mouse.move(a.x, a.y); await bob.mouse.down(); await bob.mouse.move(b.x, b.y, { steps: 6 });
    } else {
      await point(bob, 150, 300, true); await bob.keyboard.press('Enter');
      await control(bob, 'Edit canvas text'); await bob.keyboard.press('Control+a'); await bob.keyboard.insertText('Rejected text');
    }
    await expect.poll(async () => (await state(bob)).interacting).toBe(true);
    const revoked = await request.delete(owner.server + '/api/rooms/' + owner.roomId + '/invitations/' + guest.grantId, { headers: { Authorization: 'Bearer ' + owner.token } });
    expect(revoked.ok()).toBe(true);
    await expect.poll(async () => (await shared(bob)).denied).toBe(true);
    if (editKind === 'pointer') await bob.mouse.up();
    else await bob.keyboard.press('Control+Enter');
    await expect.poll(async () => (await state(bob)).interacting).toBe(false);
    expect((await shared(bob)).pending).toBe(0); expect((await shared(bob)).online).toBe(false);
    if (editKind === 'pointer') expect((await state(bob)).x).toBe(80);
    else expect((await state(bob)).text).toBe('Before');
    expect(errors).toEqual([]);
    const authoritative = await request.get(owner.server + '/api/rooms/' + owner.roomId + '/versions/0', { headers: { Authorization: 'Bearer ' + owner.token } });
    expect(authoritative.ok()).toBe(true);
    const saved = await authoritative.json(); expect(node(saved, 'a').x).toBe(80); expect(node(saved, 'text').text).toBe('Before');
  });
}
