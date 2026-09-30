# Changelog

## 0.8.0-alpha.1

- Add independent corner radii, signed ellipse arcs/rings/open contours, and custom contextual controls with direct canvas grips.
- Add centered/inside/outside stroke regions, cap/join/miter/dash-phase editing, shared drawing/picking geometry and bounded native caches.
- Retain live Boolean operands with editable operations, release, exact native flattening and per-stroke outline layers.
- Build primitives directly in Skia and preserve rational conics in native contour commands; add bounded renderer-aware SVG conversion.
- Preserve zero-extent line stroke coordinates during outlining, independent prototype clips and uniform instance-radius overrides.
- Upgrade to native schema 6 and style clipboard packet 2, retain older native/clipboard inputs, and verify persisted schema-5 rooms upgrade once.
- Add 80 native regressions, eight static browser workflows and one multi-window shape-editing workflow; retain scoped stroke-construction benchmarks.
- Preserve the existing published NuGet package documentation and release configuration. Full Figma feature/pixel parity remains unfinished.

## 0.7.0-alpha.1 — Editing workflows

- Add typed property copy/paste, single-group quick actions and a custom selective-transfer panel. Geometry, content, identities and hierarchy remain intact; transferred values materialize only their affected variable bindings.
- Add persistent instance stroke, whole-layer typography, appearance and name overrides, including inspector edits and structurally matched variant swaps.
- Add a custom before/after batch-rename panel, current-name/capture substitution, ascending/descending padded numbers, literal or linear-time regex matching and atomic stale-target preflight.
- Replace repeated per-item layer moves with stable run movement and O(n) extreme-order partitioning; preserve selected/untouched order and reject unsupported instance-child reordering explicitly.
- Complete Save's host operation and restore canvas keyboard focus after inline text and inspector teardown. The regression still saves immediately after typing.
- Introduce native schema 5 with schemas 1–4 migration; upgrade persisted shared rooms once through a durable system revision without rewriting prior history or receipts.
- Add 36 engine regressions, five static browser workflows, a real shared-property/undo case, a persisted-room migration test and an equivalent-output ordering/property-capture benchmark.
- Document exact transfer/naming semantics, ownership, migration and performance boundaries. This does not claim complete Figma parity.

## 0.6.0-alpha.1 — Collaborative editing

- Added the tenth reusable library, `VectorSpace.Collaboration`, and a persistent ASP.NET room service with guarded property/hierarchy transactions, idempotent retries, normalized layout/variables/components, and conditional own undo/redo.
- Added real Share/create/join/invite/revoke flows, server-enforced viewer/commenter/editor/owner capabilities, synchronized comments, retained remote cursors and selections, participant following, and revision download/restore.
- Added separate token-free browser/desktop recovery journals for unacknowledged or rejected work. Remote model updates respect pointer, inline-text and modal transaction boundaries; revoked mid-gesture edits roll back without escaping the input event loop.
- Replaced mutable-JSON projection with a read-only DOM and retained unchanged cell keys/values. A test-only reference oracle verifies equivalence; benchmarks report projection cost and exact delta wire size without an application-wide speed claim.
- Added actual multi-window browser and HTTP/restart acceptance, projection equivalence checks, server/container packaging and collaboration-aware build/release gates. Corrected duplicate tracing, asynchronous clipboard and stale inspector diagnostics in browser tests.
- Added collaboration/hosting documentation and refreshed architecture/security guidance. Pages remains a static client; cross-device collaboration requires a separately operated HTTPS backend with persistent disk. Native document schema remains 4.

## 0.5.0-alpha.1

- Add reusable cubic subdivision, tangent operations, point selection/drag/marquee/nudges, exact segment insertion, contour reversal/closure and transactional primitive conversion.
- Add direct point-editing inspector and context menu with robust history rebinding and cancellation.
- Fix line/arrow angle constraints and zero-height geometry; preserve subpixel SVG precision and canonical resizing.
- Improve Pen/Pencil parenting, live preview, Backspace, whole-stroke undo, simplification and tool-switch lifecycle; add persistent tools and in-drag shape adjustments.
- Add world-pivot flips/quarter-turns, keyboard axis-preserving resizing, paste in place, spacing and editable guides; keep constrained snapping on its permitted axis.
- Build editable SKPath geometry directly rather than serializing/parsing SVG per edit. Add a scoped geometry/allocation benchmark.
- Add 75 engine regressions and ten actual-browser tool/editing workflows; exact-commit CI is the source of validation status.

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
