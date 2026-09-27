# Prototype import, input and invalidation

This note records compatibility and regression details for the 0.3 prototype player. See [Prototyping](PROTOTYPING.md) for authoring, playback APIs, isolation and supported behavior.

## Compact typed values

External native documents can express Boolean and numeric values without irrelevant fields:

```json
{"type":"Boolean","boolean":true}
```

```json
{"type":"Number","number":12.5}
```

`VariableValue` remains an immutable record. Its explicit `[JsonConstructor]` supplies defaults for omitted text, numeric, Boolean and alias parameters when using generated JSON metadata. The existing parameterless constructor and object-initializer/factory API remain available.

An omitted `text` means the empty string, matching construction in C#. An explicit `"text": null` remains null and is rejected by the validator. This distinction prevents compact valid literals from failing import without accepting malformed values. The contract applies equally to variable mode values, fallbacks, prototype assignments and conditions.

Tests cover omitted true/false/numeric/string fields, colors, compact aliases, explicit null for each type, native round-tripping and execution of a compact conditional prototype. Browser fixtures intentionally retain compact values instead of being rewritten to conceal the import failure.

## Pointer ownership

A click requires a press and release over the same eligible owner. A drag beyond six screen pixels, an outside release, or navigation that changes the input scene cancels the original click. A press that navigates cannot release-activate a control at the same coordinates in the newly entered frame.

Hover compares the reactive ancestor path, not only the deepest hit node. Moving between sibling hit targets beneath one reactive parent must not emit another parent enter/leave pair. Leaving that parent emits its leave reaction; re-entering emits a new enter. These are explicit mouse-enter/mouse-leave reactions, not automatic reversible while-hover semantics.

Pointer location is retained while a transition suppresses activation. When the animation ends, the clock-driven update reconciles that location even if the pointer stopped moving. A quick move off an animating hotspot must not leave its hover state stuck. A browser regression enters an animated hotspot, moves away once and observes the leave reaction without subsequent input.

The browser pointer suite exercises these rules with actual mouse movement and buttons. Read-only state assertions observe runtime variables and frame identity; no JavaScript command or document-mutation hook performs the interaction.

## Retained player updates

`PrototypePlayer` retains two hover-path buffers across samples instead of allocating a new list and temporary LINQ result arrays on every move. It fits its presentation viewport only when the destination frame identity/dimensions or canvas size changes, or the user restarts playback.

Canvas invalidation is driven by a new playback revision, a changed animation clock while animating, a fit change or an explicit load/size refresh. Unchanged pointer movement does not request a new scene paint. Toolbar text updates only when the state changes. Transition completion publishes a revision so its final frame still renders.

The player schedules a dispatcher timer only for an active transition or the next pending delayed reaction. Unloading pauses its clock and stops the timer; disposal releases capture and renderer ownership correctly. A supplied renderer is borrowed and retains host-installed fonts.

These changes remove avoidable work; they are not a measured whole-application frame-rate claim. Hit testing, delegate dispatch, variable/variant copy-on-write transactions and Skia painting still have CPU/allocation costs. Prepared smart interpolation reuses a scene tree but can allocate changed paint strings. Arbitrary-scale, allocation-free playback is not claimed.

## Reproduce

```bash
dotnet run --project tests/VectorSpace.Tests -c Release
python3 -m unittest discover -s tests/scripts -v

# With a published application served by scripts/serve-site.py:
npx playwright test tests/browser/prototype.spec.mjs tests/browser/prototype-pointer.spec.mjs
```

CI retains exact-commit reports. The public Pages verification runs the same tests against the deployed application. Native host builds, browser tests and live-site tests are separate checks; one does not stand in for another.
