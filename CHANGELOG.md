# Changelog

## 0.4.0-alpha.1

- Add bounded PNG/JPEG/WebP importing, EXIF orientation normalization, embedded image paints, Fill/Fit/Crop/Tile, rotation and exposure/contrast/saturation controls.
- Add direct image-crop dragging and cursor-anchored wheel zoom, a thirds grid, transactional cancellation and original editable appearance studies.
- Add complete fill/effect instance overrides, per-fill blends and reordering, multiple independent outer shadows, alpha-preserving inner shadows, spread and layer blur.
- Correct gradient opacity, retain shaders/filter graphs, deduplicate decoded images with LRU byte budgets, and constrain flood-like inner-filter processing to known source footprints.
- Import local/inherited SVG gradients, stop alpha, spread and focus; bake group/viewBox scaling through descendants; export image placement/tiling/adjustments and supported filter graphs.
- Introduce schema 4 with versions 1–3 migration; prevent recursive serialization of computed matrix inverses.
- Enforce decoded-image admission before pixel allocation; avoid image-sized content-key and Base64 substring allocations, retain immutable pixels, and evict reduced native-cache budgets immediately.
- Add 77 engine regressions, six actual-browser appearance workflows and an appearance retention benchmark. Complete suites contain 310 engine, 6 publication-script and 25 browser cases.
- Keep all feature, resource and interoperability limitations explicit in `docs/APPEARANCE.md` and `docs/FEATURES.md`.

## 0.3.0-alpha.1

- Add the reusable UI-independent `VectorSpace.Prototyping` package and deterministic isolated playback state.
- Add named flow starts, click/enter/leave/press/release/key/delay reactions, ordered actions, typed conditions, variable operations and interactive variants.
- Add navigation history, modal overlay stacks, placement/backdrops, outside dismissal, scroll-to and wheel/touch scrolling.
- Add Skia instant/dissolve/move/push transitions and prepared name-matched smart interpolation; unsupported overlay smart transitions fall back to dissolve.
- Add a dedicated Uno player, contextual interaction authoring, consent-gated external links and an original editable prototype playground.
- Preserve the editor viewport/history/save state during playback; validate action batches before committing private runtime mutations.
- Upgrade native JSON to schema 3 with version-1/2 migration, legacy links, reference remapping and inherited/local component interaction overrides.
- Fix source-generated import of compact immutable variable literals with explicit constructor defaults; keep explicitly null text invalid.
- Retain hover-path buffers, skip unchanged player invalidations and avoid repeated viewport fitting on pointer movement.
- Reconcile retained pointer location when transitions finish so quick hover exits cannot remain stuck without further input.
- Add 65 engine regressions and ten prototype browser cases, including hover paths, canceled clicks, release suppression after press navigation and animated hover exits. Complete suites contain 233 engine cases and 19 browser cases.
- Document exact prototype semantics, resource limits and remaining Figma compatibility boundaries. Full Figma parity is not claimed.

## 0.2.0-alpha.2

- Normalize missing Alt pointer modifiers from Uno keyboard state for resize, drawing, duplication and tangent gestures.
- Preserve hug/fill on untouched sizing axes during handle drags; horizontal resizing now reflows wrapping hug-height frames.
- Keep opposite edges and centers stationary at min/max constraints, including nested rotation/reflection.
- Fix proportional side-handle shrinking and centered proportional resizing; add reusable allocation-free resize geometry.
- Add 23 native regressions and two browser workflows, for 168 engine tests and nine browser tests.
- Correct the prior documentation: gesture baselines are implemented; text-baseline alignment is not.

## 0.2.0-alpha.1

- Advanced wrap/grid auto-layout, constrained sizing, scoped hierarchy selection and stable baseline gesture transforms.
- Local component sets/variants, structural override matching, stable nested instance IDs, transitive synchronization skips, cycle and expansion limits.
- Typed variables, aliases, inherited modes, property bindings, local instance overrides, reset and dependency-aware clipboard interchange.
- Native format 2 with version-1 migration; existing appearance/export workflows retained.
- Indexed gesture snapping, retained geometry/text caches, conservative viewport culling, and selection-only layer-list updates.
- Expanded engine and browser tests, design-system guide and reproducible CPU snapping benchmark.
- Reviewed dependency PRs integrated with managed/native Skia ABI alignment and retained hidden Pages assets.

## 0.1.0-alpha.1

Initial reusable Uno/Skia editor, original editable Aether sample, transactional vector tools, compact workbench, contextual inspector, JSON/SVG interchange, PNG export, local components/comments, prototype navigation, browser/desktop hosts, package output, CI and Pages configuration.

See `docs/FEATURES.md` for explicit limits. This line does not claim complete Figma compatibility.
