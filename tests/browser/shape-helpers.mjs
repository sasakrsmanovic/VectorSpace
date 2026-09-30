import { expect } from '@playwright/test';
import fs from 'node:fs/promises';
import { waitForSkiaControl } from './skia-control-readiness.mjs';

export const state = page => page.evaluate(() => globalThis.__vectorSpaceState);
export const control = (page, name, type) => waitForSkiaControl(page, name, type);
export async function click(page, name, type) { const c = await control(page, name, type); await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2); }
export async function input(page, name, value) { await click(page, name, 'TextBox'); await page.keyboard.press('Control+a'); await page.keyboard.insertText(String(value)); }
export async function inspect(page, name, type) {
  await page.mouse.move(1560, 670); await page.mouse.wheel(0, -9000); await page.waitForTimeout(200);
  for (let i = 0; i < 30; i++) {
    if (await page.evaluate(({ name, type }) => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === name && (!type || c.type === type) && c.enabled && c.height > 12), { name, type })) return;
    await page.mouse.wheel(0, 180); await page.waitForTimeout(180);
  }
  throw new Error('Inspector control not found: ' + name);
}
export async function field(page, name, value) { await inspect(page, name, 'TextBox'); await input(page, name, value); await page.keyboard.press('Enter'); }
export async function choose(page, name, value) { await inspect(page, name, 'ComboBox'); await click(page, name, 'ComboBox'); await click(page, value, 'ComboBoxItem'); }
export async function screen(page, x, y) { const c = await control(page, 'Design canvas'); const s = await state(page); return { x: c.x + s.panX + x * s.zoom, y: c.y + s.panY + y * s.zoom }; }
export async function point(page, x, y, deep = true) { const p = await screen(page, x, y); if (deep) await page.keyboard.down('Control'); await page.mouse.click(p.x, p.y); if (deep) await page.keyboard.up('Control'); }
export async function action(page, name) {
  await page.keyboard.press('Control+k'); await input(page, 'Search quick actions', name);
  await expect.poll(() => page.evaluate(() => (globalThis.__vectorSpaceControls ?? []).find(c => c.name === 'Search quick actions')?.value)).toBe(name);
  const search = await control(page, 'Search quick actions', 'TextBox');
  // An identically named inspector button remains in the diagnostic tree behind
  // the modal. Resolve only the visible results panel, never the background.
  const c = await waitForSkiaControl(page, name, 'StudioButton', {
    within: { x: search.x, y: search.y + search.height, width: search.width, height: 380 }
  });
  await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2);
  await expect.poll(() => page.evaluate(() => (globalThis.__vectorSpaceControls ?? []).some(c => c.name === 'Search quick actions'))).toBe(false);
}
export function fixture(children) {
  return { formatVersion: 6, id: 'shape-fixture', name: 'Shape acceptance', pages: [{ id: 'shapes', name: 'Shapes', nodes: [
    { id: 'stage', kind: 'Frame', name: 'Stage', width: 800, height: 600, fills: [{ color: '#FFFFFF' }], children: children ?? [
      { id: 'rect', name: 'Independent rectangle', x: 150, y: 160, width: 200, height: 160, cornerRadius: 12, fills: [{ color: '#F24822' }], strokes: [{ width: 12, color: '#0000FF' }] },
      { id: 'ellipse', kind: 'Ellipse', name: 'Arc ellipse', x: 460, y: 160, width: 160, height: 160, fills: [{ color: '#14AE5C' }], strokes: [{ width: 6, color: '#000000' }] }
    ] }
  ] }] };
}
export async function open(page, document = fixture()) {
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.goto('?test=1'); await page.waitForFunction(() => globalThis.__vectorSpaceState?.ready, null, { timeout: 150000 });
  await control(page, 'Design canvas'); await page.mouse.click(650, 240);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await picker).setFiles({ name: 'shapes.vectorspace', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(document)) });
  await expect.poll(async () => (await state(page)).page).toBe('Shapes');
}
export async function save(page, filename) {
  const pending = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const download = await pending;
  await fs.mkdir('artifacts', { recursive: true }); const path = 'artifacts/' + filename; await download.saveAs(path);
  await expect.poll(async () => (await state(page)).saving).toBe(false);
  return JSON.parse(await fs.readFile(path, 'utf8'));
}
export async function grip(page, index) {
  const c = await control(page, 'Design canvas'); const s = await state(page); const h = s.shapeHandles[index];
  if (!h) throw new Error('Shape grip not available: ' + index);
  return { x: c.x + h.x, y: c.y + h.y, zoom: s.zoom };
}
export function child(document, id) { return document.pages[0].nodes[0].children.find(n => n.id === id); }
