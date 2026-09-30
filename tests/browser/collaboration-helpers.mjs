import { test as base, expect } from '@playwright/test';
import fs from 'node:fs/promises';
import { waitForSkiaControl } from './skia-control-readiness.mjs';

export const test = base.extend({
  peer: async ({ browser }, use, testInfo) => {
    const contexts = [];
    await use(async () => {
      const context = await browser.newContext({ baseURL: testInfo.project.use.baseURL, viewport: { width: 1680, height: 1000 }, acceptDownloads: true });
      contexts.push(context);
      return context.newPage();
    });
    // Playwright Test owns tracing for every context. Starting/stopping it here
    // conflicts with retain-on-failure and prevents peer scenarios from executing.
    for (const context of contexts) await context.close();
  }
});
export { expect };
export const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
export const shared = async page => (await state(page)).collaboration;
export async function control(page, name, type) {
  return waitForSkiaControl(page, name, type);
}
export async function click(page, name, type) { const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
export async function input(page, name, value, type = 'TextBox') { await click(page, name, type); await page.keyboard.press('Control+a'); await page.keyboard.insertText(value); }
export async function screen(page, x, y) { const c = await control(page, 'Design canvas'); const s = await state(page); return { x: c.x + s.panX + x * s.zoom, y: c.y + s.panY + y * s.zoom }; }
export async function point(page, x, y, deep = false) { const p = await screen(page, x, y); if (deep) await page.keyboard.down('Control'); await page.mouse.click(p.x, p.y); if (deep) await page.keyboard.up('Control'); }
export async function select(page, id) {
  await page.keyboard.press('v'); await point(page, id === 'a' ? 160 : 440, 130, true);
  await expect.poll(async () => (await state(page)).id).toBe(id);
}
export async function synced(page, revision) { await expect.poll(async () => (await shared(page)).pending).toBe(0); await expect.poll(async () => (await shared(page)).revision).toBeGreaterThanOrEqual(revision); }
export function fixture() {
  return { formatVersion: 4, id: 'shared-fixture', name: 'Shared design', pages: [{ id: 'shared-page', name: 'Shared canvas', nodes: [
    { id: 'stage', name: 'Stage', kind: 'Frame', width: 800, height: 600, fills: [{ color: '#FFFFFF' }], children: [
      { id: 'a', name: 'Card A', x: 80, y: 80, width: 160, height: 100, fills: [{ color: '#F24822' }] },
      { id: 'b', name: 'Card B', x: 360, y: 80, width: 160, height: 100, fills: [{ color: '#0D99FF' }] },
      { id: 'text', name: 'Title', kind: 'Text', text: 'Before', x: 80, y: 280, width: 300, height: 50 }
    ] }
  ] }], comments: [{ id: 'thread', pageId: 'shared-page', anchor: { x: 580, y: 300 }, author: 'Review', text: 'Shared review', replies: [] }] };
}
export function server() {
  if (!process.env.VECTORSPACE_COLLAB_URL || !process.env.VECTORSPACE_CREATE_KEY) throw new Error('Start the real collaboration service with scripts/run-collaboration-browser-tests.py');
  return process.env.VECTORSPACE_COLLAB_URL;
}
export async function room(request, document = fixture()) {
  const response = await request.post(server() + '/api/rooms', { headers: { 'X-VectorSpace-Create-Key': process.env.VECTORSPACE_CREATE_KEY }, data: { document: JSON.stringify(document), name: 'Alice' } });
  expect(response.ok()).toBe(true); const grant = await response.json(); return { ...grant, server: server() };
}
export async function invite(request, owner, name = 'Bob', role = 'Editor') {
  const response = await request.post(owner.server + '/api/rooms/' + owner.roomId + '/invitations', { headers: { Authorization: 'Bearer ' + owner.token }, data: { label: name, role } });
  expect(response.ok()).toBe(true); return { ...await response.json(), server: owner.server };
}
export function fragment(grant) { return '#collaboration=' + Buffer.from(JSON.stringify({ server: grant.server, room: grant.roomId, token: grant.token })).toString('base64url'); }
export async function join(page, grant, name) {
  // A newly created sibling page can leave this one backgrounded during startup.
  await page.bringToFront();
  await page.goto('?test=1' + fragment(grant));
  await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await input(page, 'Collaboration display name', name);
  expect(page.url()).not.toContain('#collaboration=');
  await click(page, 'Join file'); await click(page, 'Continue');
  await expect.poll(async () => (await shared(page)).connected).toBe(true);
  await expect.poll(async () => (await state(page)).page).toBe('Shared canvas');
  await expect.poll(async () => (await shared(page)).role).toBe(grant.role);
  // Joining must release both the modal and command boundary before authoring.
  await expect.poll(async () => (await shared(page)).dialogDepth).toBe(0);
  await expect.poll(async () => (await shared(page)).asyncDepth).toBe(0);
  await expect.poll(async () => (await shared(page)).boundaryBlocked).toBe(false);
  await control(page, 'Design canvas');
}
export async function document(page, name = 'shared-document.vectorspace') {
  // Leave Comment/drawing mode through the real palette before focusing canvas.
  // Otherwise this preparatory click opens a new comment editor instead of Save.
  await click(page, 'Move (V)'); await expect.poll(async () => (await state(page)).tool).toBe('Move');
  await page.mouse.click(700, 200); const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download = await pending; await fs.mkdir('artifacts', { recursive: true }); const path = 'artifacts/' + name; await download.saveAs(path);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
export async function downloadCopy(page, action, name) {
  const pending = page.waitForEvent('download'); await action(); const download = await pending;
  await fs.mkdir('artifacts', { recursive: true }); const path = 'artifacts/' + name; await download.saveAs(path);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
export function node(document, id) { return document.pages[0].nodes[0].children.find(n => n.id === id); }
