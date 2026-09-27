# Changelog

## 0.2.0-alpha.2

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
