# Validation and delivery

The engine regression runner covers 145 scenarios: transforms, layout/constraints, snapping, document validation, safe SVG parsing, transactional history, selection/clipboard, components, hit testing, PNG pixels, Boolean paths, and sample rendering. The expanded suite includes wrap/grid/baseline and scoped selection behavior, variable aliases/modes/bindings, instance identity/override stability, variant swaps, clipboard dependencies, cycle/expansion limits, actual variable-driven PNG pixels, and indexed snapping equivalence/allocation checks.

Published-browser acceptance tests use Chromium and real pointer/keyboard events. They cover creation, nudging, duplication/deletion, undo/redo, editable-document download, IndexedDB recovery after reload, cursor-anchored zoom, pen/freehand-related state, compact layout, resize handles, and cancelling/restarting an unfinished pen transaction. Read-only `?test=1` diagnostics are used for assertions; tests do not mutate document state through JavaScript.

`Build` validates the platform-independent engines, publishes the actual Uno WebAssembly application, runs browser acceptance, and creates eight reusable package artifacts. `Desktop` independently compiles Windows, Linux and macOS hosts so a native runner queue does not hold up an already verified browser deployment. Native compilation is not equivalent to native interaction coverage.

`Pages` deploys only successful main-branch Build artifacts and then runs the browser suite against the public URL. `build-info.json` records the deployed source commit. The actions logs and retained screenshots/traces are the source of truth for each run; workflow configuration by itself is not evidence of a passing test or a live deployment.

The design-system browser suite opens an actual native document through the file picker and drives variable modes, variant property dropdowns and local-variable authoring through actual mouse/keyboard events. Read-only named-control bounds support locating Skia-rendered controls and popups; the diagnostics timer exists only in opt-in test mode. A configured test is not a passing result: consult the corresponding Actions run.

The first desktop/browser screenshot was captured from an actual running application, not a static UI mockup. Pixel-identical Figma parity, broad assistive-technology certification and every native operating-system interaction are not claimed.
