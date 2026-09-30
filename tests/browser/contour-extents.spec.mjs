import { test, expect } from '@playwright/test';
import { child, fixture, open, point, save, state } from './shape-helpers.mjs';

function worldPoint(node, p) {
  const x = (p.x * node.width / node.pathWidth - node.width / 2) * (node.flipX ? -1 : 1);
  const y = (p.y * node.height / node.pathHeight - node.height / 2) * (node.flipY ? -1 : 1);
  const a = (node.rotation ?? 0) * Math.PI / 180;
  return { x: node.x + node.width / 2 + x * Math.cos(a) - y * Math.sin(a),
    y: node.y + node.height / 2 + x * Math.sin(a) + y * Math.cos(a) };
}
for (const kind of ['Line', 'Arrow']) {
  test(`${kind} with a zero extent converts to editable contours without moving and retains keyboard resizing`, async ({ page }) => {
    const vertical = kind === 'Arrow';
    const original = { id: 'thin-vector', kind, name: 'Zero extent ' + kind, x: 200, y: 180,
      width: vertical ? 0 : 180, height: vertical ? 180 : 0, fills: [], strokes: [{ color: '#14AE5C', width: 8 }] };
    await open(page, fixture([original]));
    await point(page, original.x + original.width / 2, original.y + original.height / 2);
    await expect.poll(async () => (await state(page)).id).toBe(original.id);
    await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(true);
    const converted = child(await save(page, `converted-zero-${kind}.vectorspace`), original.id);
    expect(converted.kind).toBe('Path'); expect(converted.pathWidth).toBeGreaterThan(0); expect(converted.pathHeight).toBeGreaterThan(0);
    const contours = converted.contours ?? [{ points: converted.points, closed: converted.closed }];
    expect(contours).toHaveLength(vertical ? 2 : 1);
    const [a, b] = contours[0].points.map(p => worldPoint(converted, p.position));
    expect(a.x).toBeCloseTo(original.x, 5); expect(a.y).toBeCloseTo(original.y, 5);
    expect(b.x).toBeCloseTo(original.x + original.width, 5); expect(b.y).toBeCloseTo(original.y + original.height, 5);
    await page.keyboard.press('Enter'); await expect.poll(async () => (await state(page)).vectorEditing).toBe(false);
    await page.keyboard.press(vertical ? 'Control+Alt+ArrowDown' : 'Control+Alt+ArrowRight');
    const resized = child(await save(page, `resized-zero-${kind}.vectorspace`), original.id);
    const edge = resized.contours?.[0].points ?? resized.points;
    const start = worldPoint(resized, edge[0].position), end = worldPoint(resized, edge[1].position);
    expect(vertical ? end.y - start.y : end.x - start.x).toBeCloseTo(181, 5);
    await page.keyboard.press('Control+z'); await page.keyboard.press('Control+z');
    const restored = child(await save(page, `restored-zero-${kind}.vectorspace`), original.id);
    expect(restored).toMatchObject({ kind, x: original.x, y: original.y, width: original.width, height: original.height });
    expect(restored.contours ?? null).toBeNull();
  });
}
