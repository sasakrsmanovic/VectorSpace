# Architecture

## Package boundaries

`Core` owns plain document and geometry types. `Layout` and `Documents` depend on Core. `Editing` composes them into selection, viewport, transactional history components, typed variables and local variants. `Skia` provides native rendering and geometry operations. `Controls` contains document-independent Uno primitives. `Editor` embeds the canvas and interaction logic; `Workbench` composes the application workspace. `App` supplies browser/desktop storage adapters and startup.

Engine packages target `net10.0`; Uno-facing libraries target `net10.0-browserwasm` and `net10.0-desktop`. The dependency graph is acyclic.

## Document and editing contract

Nodes have stable IDs and parent-local double-precision transforms. Parent references are reconstructed after deserialization, not serialized. Cloning can retain or regenerate IDs and remaps internal references.

`EditorSession` owns selection, viewport and bounded undo/redo history. A gesture previews changes inside one transaction and commits one before/after record. Cancellation restores document and selection. History uses entry-count and approximate serialized-character budgets; it is not unbounded.

Snapshots are deliberately straightforward for this alpha. Structural sharing, incremental history and a full hit-test spatial index are future scalability work, not a claim of million-node performance today.

## Rendering

`SceneRenderer` owns cached paths/typefaces. Shared path construction feeds rendering and SVG export. Traversal applies nested transforms, clipping, paints, blends and the first drop shadow, with conservative node-level viewport culling and bounded LRU geometry/text caches. The Uno surface derives from `SKCanvasElement` and renders into Uno's Skia canvas; there is no parallel DOM implementation.

Handles are drawn in screen coordinates; geometry/snapping use world coordinates and convert into parent coordinates for model updates. PNG export uses a separate surface without editor chrome.

## Storage and trust

`IWorkspaceStorage` separates recovery, open and save from the UI. Browser `[JSImport]` calls wrap IndexedDB, a file input and Blob downloads. JavaScript contains neither a duplicate model nor an editor UI. Desktop uses local files and Uno file pickers.

System.Text.Json source generation avoids reflection-dependent document metadata. SVG XML parsing prohibits DTDs and external resolution. Unsupported content is reported instead of executed. Files are validated before replacing the current document.

## Validation

The engine runner exits nonzero on failure and tests geometry, layout, serialization, hostile input, history, components, hit testing, PNG pixels and Boolean paths. Playwright tests use published WebAssembly with real pointer/keyboard events. `?test=1` enables read-only state diagnostics, avoiding dependencies on private Uno DOM internals.

Actions retains logs, screenshots, traces, browser outputs and NuGet artifacts. These are evidence of the tested scenarios, not a substitute for native UI testing or a complete Figma parity audit.

## Design-system commit ordering

A commit validates variable graphs, resolves bound properties, synchronizes acyclic component definitions, resolves instance-context bindings, arranges layout, validates the resulting document and captures history. Exceptions retain the pre-edit rollback snapshot. Definition fingerprints and scoped identity maps avoid rebuilding unchanged instances or sharing descendant IDs between separate nested occurrences. Gesture snapping builds an immutable coordinate index once and performs indexed queries without per-target pointer-move allocations. See [design systems](DESIGN_SYSTEMS.md) and [performance](PERFORMANCE.md).
