/**
 * Read-only readiness for Uno's Skia-rendered controls.
 * Wait for matching bounds in two distinct publications and an unobstructed DOM
 * target. Optional containment distinguishes a modal result from an identically
 * named control painted behind it. No document mutation or click retries.
 */
export async function waitForSkiaControl(page, name, type, { timeout = 20_000, within } = {}) {
  if (!Number.isFinite(timeout) || timeout <= 0) throw new RangeError('A positive readiness timeout is required.');
  if (within && (![within.x, within.y, within.width, within.height].every(Number.isFinite) || within.width <= 0 || within.height <= 0))
    throw new RangeError('The control scope must be a finite positive rectangle.');
  return page.evaluate(({ name, type, timeout, within }) => new Promise((resolve, reject) => {
    let timer, deadline, finished = false;
    let previousPublication, previousBounds, stablePublications = 0;
    let reason = 'the named control has not been published';
    const finish = (error, value) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer); clearTimeout(deadline);
      if (error) reject(error); else resolve(value);
    };
    deadline = setTimeout(() => finish(new Error(`Control "${name}" did not become input-ready: ${reason}.`)), timeout);
    const sample = () => {
      if (finished) return;
      try {
        const publication = globalThis.__vectorSpaceControls;
        const candidate = Array.isArray(publication)
          ? publication.filter(c => c.name === name && (!type || c.type === type) && c.enabled &&
            (!within || c.x + c.width / 2 >= within.x && c.x + c.width / 2 <= within.x + within.width &&
              c.y + c.height / 2 >= within.y && c.y + c.height / 2 <= within.y + within.height)).at(-1)
          : undefined;
        const valid = candidate && [candidate.x, candidate.y, candidate.width, candidate.height].every(Number.isFinite) &&
          candidate.width > 1 && candidate.height > 1;
        const x = valid ? candidate.x + candidate.width / 2 : -1;
        const y = valid ? candidate.y + candidate.height / 2 : -1;
        const inside = valid && x >= 0 && y >= 0 && x < innerWidth && y < innerHeight;
        const top = inside ? document.elementFromPoint(x, y) : null;
        const reachable = top && /^(CANVAS|INPUT|TEXTAREA|BUTTON|SELECT)$/.test(top.tagName);
        if (!valid || !inside || !reachable) {
          stablePublications = 0; previousPublication = undefined; previousBounds = undefined;
          reason = !candidate ? 'the named enabled control is absent from the requested scope'
            : !inside ? 'its center is outside the viewport'
            : `its center is covered by ${top?.tagName ?? 'no rendered element'}`;
        } else if (publication !== previousPublication) {
          const bounds = [candidate.type, candidate.x, candidate.y, candidate.width, candidate.height].join('|');
          stablePublications = bounds === previousBounds ? stablePublications + 1 : 1;
          previousPublication = publication; previousBounds = bounds;
          reason = 'waiting for a second stable layout publication';
          if (stablePublications >= 2) { finish(null, candidate); return; }
        }
      } catch (error) { finish(error); return; }
      timer = setTimeout(sample, 20);
    };
    sample();
  }), { name, type, timeout, within });
}
