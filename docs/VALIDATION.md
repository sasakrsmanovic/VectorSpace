# Validation and delivery

## Regression suites

The engine regression runner contains **376 cases**. It covers affine transforms, wrapping/grid layout and constraints, snapping, native document validation, safe SVG parsing, transactional history, selection and clipboard dependencies, components and variants, hit testing, PNG pixels, Boolean paths and sample rendering. Prototype coverage includes isolation, typed action execution and rollback, navigation, overlays, timers, URL policy, scrolling, interpolation, rendering and reference remapping.

**Fourteen compact-JSON regressions** distinguish omitted optional literal fields from explicit nulls. Boolean, number, string and color values round-trip through the source-generated serializer. A compact conditional prototype imports, navigates and restarts without changing its source document. An explicitly null text field remains invalid; the fix does not relax document validation.

The six Python publication tests validate static asset collection, source/output separation, version resolution and provenance metadata. Indexed snapping is checked against its linear reference, including a seeded randomized equivalence test and an independently retained synthetic CPU benchmark.

Published-browser acceptance contains **33 Chromium cases** using real pointer, keyboard, file-picker and clipboard input. Nine cover the editor/design-system/resize workflows; ten cover prototype playback, authoring and input boundaries; six exercise image/effect/SVG authoring and the appearance playground; eight cover tool activation, shape creation, point editing, guides and clipboard placement. Read-only `?test=1` diagnostics expose state and control bounds for the Skia-rendered UI. Tests do not execute document mutations through JavaScript.

Counts describe the suite, not proof of a passing build. Check the Actions run for the exact source commit.

## Appearance coverage

Seventy-seven new engine cases cover orientation in all eight JPEG configurations, PNG/JPEG/WebP import, header/pixel limits, decoded cache ownership and dynamic budgets, actual image/gradient/effect pixels, complete appearance overrides, clipboard/migration, native matrix serialization and SVG gradient/viewBox/group semantics. Inner-shadow regressions check translucent alpha, independent outer shadows and footprint invalidation. A filtered-child test passes a real viewport to verify culling does not discard required offscreen inputs.

Seventeen admission-specific regressions additionally verify header-budget rejection before pixel decoding, immutable image lifetime, allocation-bounded equal-content lookup, oversized-key rejection, Base64 spans and immediate gradient/effect cache budget reductions.

Six browser cases use actual image/file pickers, inspector choices, crop drag/wheel/cancel, effects and quick actions. Screenshot pixel assertions check image letterboxing/color and SVG gradients. Read-only diagnostics do not expose mutation commands. The appearance benchmark checks identical cached/rebuilt pixels and zero repeated decode/shader/filter construction on stable frames; it does not enforce a timing threshold.

## Browser coverage

The original editor cases exercise creation, nudging, duplication/deletion, undo/redo, editable-document download, IndexedDB recovery, cursor-anchored zoom, pen transaction cancellation and compact layout. Design-system cases drive variable modes, variant selectors, local-variable authoring and cross-document clipboard dependencies through actual controls.

Resize cases import native fixtures, drag actual handles, test anchored min/max limits, modifier behavior, undo/redo and saved hug-height state. Native cases cover every handle under rotation/reflection, untouched hug/fill axes, proportional side shrinking and wrapping reflow. See [resizing semantics](RESIZING.md).

Prototype cases cover release-click navigation, editor viewport preservation, modal click consumption and Escape ordering, private variables and saved-file isolation, scrolling and keyboard navigation, inspector edits, deadlines and player disposal. Pointer cases verify one hover entry while crossing sibling hit targets, hover leave, drag/release cancellation, prevention of release activation in a frame entered during the same press and hover reconciliation after an animation ends without further mouse input. The Back-navigation test checks the exact saved scroll offset after observing the returned frame rather than reading a stale diagnostics sample. See [prototype behavior](PROTOTYPING.md) and [import and input validation](PROTOTYPE_VALIDATION.md).

## Build, native hosts and deployment

**Build** validates the platform-independent engines and publication scripts, publishes the actual Uno WebAssembly application, runs browser acceptance and creates **nine reusable package artifacts**. Benchmark output, browser reports, screenshots, traces, packages and source snapshots are retained as Actions artifacts.

**Desktop** independently compiles Windows, Linux and macOS hosts so a native runner queue does not hold up an already verified browser deployment. Compilation is not equivalent to native interaction coverage. The browser suite currently uses Chromium; other browser engines are not certified by these runs.

**Pages** deploys only successful main-branch Build artifacts, checks their source provenance and runs the same browser suite against the public URL. `build-info.json` records the deployed source commit and project version. Workflow configuration or an artifact upload alone does not prove deployment; the deployment and public verification results must be checked.

Generated `.nupkg` and `.snupkg` files are downloadable build outputs, not evidence of publication to NuGet.org. A tagged release is a separate workflow.

## Evidence and boundaries

Actions logs and retained screenshots/traces are the source of truth for each run. Screenshots are captured from the real application, not a static mockup. Browser test mode uses a diagnostics timer that does not run in ordinary usage.

Full Figma feature compatibility, pixel identity, complete native interaction coverage, broad assistive-technology certification, native `.fig` interoperability and whole-application FPS improvements are not claimed. Performance measurements must retain their workload, runtime, allocation scope and exclusions.

## Tool editing (0.5)

Sixty-six engine regressions cover cubic subdivision, tangent semantics, conversion, reflection, precision and native path equivalence. Eight browser workflows use real quick actions, mouse/keyboard/file input and downloaded native documents to verify all sixteen tool activations, shape adjustments, multi-anchor editing, subdivision, marquee/cancellation, pen lifecycle, guides and paste-in-place/keyboard resizing. Suite counts describe available tests; final-commit results must be checked separately.
