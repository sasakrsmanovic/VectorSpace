# Contributing

Use the pinned .NET SDK and run the engine suite before opening a pull request. Changes to pointer behavior, layout, typography or controls should include a browser trace/screenshot and a regression test where practical.

Keep the dependency graph directed. Controls remain independent of document/editor state. Do not add application globals to reusable packages.

Document mutations belong in `EditorSession.Edit` or begin/commit/cancel transactions. A continuous gesture produces one undo entry, not one per pointer move. Preserve parent-local coordinates and stable identifiers; remap prototype/component references when cloning.

Dispose owned Skia objects, never borrowed cached paths. Validate untrusted files before changing the active document. Do not execute imported scripts, resolve external SVG resources or add telemetry/network services without an explicit review.

A parity feature is complete only when model, command, UI, persistence, undo behavior and tests agree. Update `docs/FEATURES.md` honestly; a stub or static mockup is not an implemented feature.
