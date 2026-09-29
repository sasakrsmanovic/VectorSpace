import test from 'node:test';
import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { waitForSkiaControl } from '../browser/skia-control-readiness.mjs';

const base = { name: 'Join file', type: 'Button', x: 100, y: 200, width: 100, height: 30, enabled: true };

async function fixture(run) {
  const saved = new Map(['document', 'innerWidth', 'innerHeight', '__vectorSpaceControls']
    .map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  let top = 'CANVAS';
  Object.defineProperty(globalThis, 'document', { configurable: true, value: {
    elementFromPoint: () => top ? { tagName: top } : null
  } });
  Object.defineProperty(globalThis, 'innerWidth', { configurable: true, value: 800 });
  Object.defineProperty(globalThis, 'innerHeight', { configurable: true, value: 600 });
  const publish = (changes = {}) => {
    globalThis.__vectorSpaceControls = Object.freeze([Object.freeze({ ...base, ...changes })]);
  };
  const page = { evaluate: (callback, args) => callback(args) };
  try { await run({ page, publish, cover: value => { top = value; } }); }
  finally {
    for (const [key, descriptor] of saved) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
    }
  }
}

test('requires fresh matching publications, not repeated reads of stale coordinates', () => fixture(async ({ page, publish }) => {
  publish();
  let resolved = false;
  const result = waitForSkiaControl(page, base.name, 'Button', { timeout: 1500 }).then(c => { resolved = true; return c; });
  await delay(60);
  assert.equal(resolved, false);
  publish();
  assert.equal((await result).y, base.y);
}));

test('published controls remain blocked while the bootstrap screen intercepts input', () => fixture(async ({ page, publish, cover }) => {
  cover('DIV'); publish();
  let resolved = false;
  const result = waitForSkiaControl(page, base.name, undefined, { timeout: 1500 }).then(c => { resolved = true; return c; });
  await delay(40); publish(); await delay(40);
  assert.equal(resolved, false);
  cover('CANVAS'); publish(); await delay(40);
  assert.equal(resolved, false);
  publish(); assert.equal((await result).name, base.name);
}));

test('moving modal bounds reset stability and return the final coordinates', () => fixture(async ({ page, publish }) => {
  publish();
  let resolved = false;
  const result = waitForSkiaControl(page, base.name, 'Button', { timeout: 1500 }).then(c => { resolved = true; return c; });
  await delay(40); publish({ y: 175 }); await delay(40);
  assert.equal(resolved, false);
  publish({ y: 175 }); assert.equal((await result).y, 175);
}));

test('disabled and offscreen controls cannot become input-ready', () => fixture(async ({ page, publish }) => {
  publish({ enabled: false });
  let resolved = false;
  const result = waitForSkiaControl(page, base.name, 'Button', { timeout: 1500 }).then(c => { resolved = true; return c; });
  await delay(40); publish({ y: 700 }); await delay(40); publish({ y: 700 }); await delay(40);
  assert.equal(resolved, false);
  publish(); await delay(40); publish(); assert.equal((await result).enabled, true);
}));

test('native text overlays remain actionable after stable diagnostics', () => fixture(async ({ page, publish, cover }) => {
  cover('INPUT'); publish({ type: 'TextBox' });
  const result = waitForSkiaControl(page, base.name, 'TextBox', { timeout: 1500 });
  await delay(40); publish({ type: 'TextBox' });
  assert.equal((await result).type, 'TextBox');
}));

test('blocked input times out explicitly without retrying any click', () => fixture(async ({ page, publish, cover }) => {
  cover('DIV'); publish();
  await assert.rejects(waitForSkiaControl(page, base.name, undefined, { timeout: 60 }),
    /Join file.*covered by DIV/);
}));
