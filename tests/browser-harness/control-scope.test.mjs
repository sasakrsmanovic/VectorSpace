import test from 'node:test';
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { waitForSkiaControl } from '../browser/skia-control-readiness.mjs';

async function fixture(run) {
  const keys = ['document', 'innerWidth', 'innerHeight', '__vectorSpaceControls'];
  const original = new Map(keys.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  Object.defineProperty(globalThis, 'document', { configurable: true, value: { elementFromPoint: () => ({ tagName: 'CANVAS' }) } });
  Object.defineProperty(globalThis, 'innerWidth', { configurable: true, value: 800 });
  Object.defineProperty(globalThis, 'innerHeight', { configurable: true, value: 600 });
  const modal = { name: 'Outline stroke', type: 'StudioButton', x: 150, y: 240, width: 320, height: 36, enabled: true };
  const behind = { ...modal, x: 630, y: 450, width: 140 };
  const publish = (includeModal = true) => {
    globalThis.__vectorSpaceControls = Object.freeze(includeModal ? [{ ...modal }, { ...behind }] : [{ ...behind }]);
  };
  try { await run({ page: { evaluate: (f, args) => f(args) }, publish, modal }); }
  finally {
    for (const [key, descriptor] of original) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
    }
  }
}
const within = { x: 150, y: 210, width: 320, height: 370 };

test('modal containment excludes a later identically named inspector control', () => fixture(async ({ page, publish, modal }) => {
  publish();
  const result = waitForSkiaControl(page, modal.name, modal.type, { within, timeout: 1500 });
  await delay(40); publish();
  const selected = await result;
  assert.equal(selected.x, modal.x); assert.equal(selected.y, modal.y);
}));

test('an absent modal result cannot fall back to a visible background control', () => fixture(async ({ page, publish, modal }) => {
  publish(false);
  await assert.rejects(waitForSkiaControl(page, modal.name, modal.type, { within, timeout: 80 }), /absent from the requested scope/);
}));

test('invalid containment fails before attempting a browser read', async () => {
  let invoked = false;
  await assert.rejects(waitForSkiaControl({ evaluate() { invoked = true; } }, 'Outline stroke', undefined,
    { within: { x: 0, y: 0, width: NaN, height: 20 } }), RangeError);
  assert.equal(invoked, false);
});
