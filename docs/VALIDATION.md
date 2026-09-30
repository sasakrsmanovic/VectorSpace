# Validation and delivery

## Source coverage

The engine runner contains **515 cases**: 421 prior cases and 94 shape/integration regressions. Shape tests cover independent radii and clipping, signed arcs/ring holes, native conic round-trips, aligned/dashed stroke regions, zero-extent line outlines, Boolean identities/undo, retained-cache invalidation, transformed gestures, source/instance corner overrides, stable primitive anchor order, the destructive Boolean compatibility API and bounded validation. Pixel comparisons and high-resolution curve-position checks supplement object-state assertions.

The collaboration model runner contains **20 cases**. The shared-editor runner contains **23**, including a projection oracle with **59 equivalence/reuse assertions**. Six publication tests and **nine input-readiness harness tests** cover build metadata/assets, unobstructed Skia control readiness and modal containment when the inspector has an identically named control.

**Ten black-box HTTP tests** start the actual ASP.NET process. They test authorization, roles, revocation, concurrent edits, idempotence, presence, long polls, journal recovery and schema-4/5 room upgrades to schema 6. The schema-5 test edits upgraded stroke alignment, restarts the service and reads earlier history. No in-memory transport replaces the server.

These counts describe source coverage. Passing results require the matching commit's Actions run and retained reports; a source count alone is not a delivery claim.

## Browser workflows

The complete suite contains **61 Chromium cases**: 48 static editor workflows and 13 independent multi-window collaboration workflows tagged `@collaboration`. Eight new static cases use actual corner/arc fields and grips, Alt/Shift modifiers, capture cancellation, stroke controls/property transfer, live Boolean editing/flatten/release, stroke outlines and the original Shape playground. They inspect downloaded native files and rendered pixels as well as read-only diagnostics.

A two-client shape case verifies corner changes and own undo retain a peer's later move; stroke outline children then synchronize across the actual service. The other collaboration cases exercise consent/joining, roles, presence/following, comments, offline reconnect/recovery, revision restoration and revocation during pointer/text transactions.

The prior 40 static cases retain editing, text-save/re-entry, image/SVG, auto-layout, design-system and prototype checks. Tests use real pointer, keyboard, file-picker and clipboard input. `?test=1` provides read-only coordinates/state, not a document mutation API. Credentials are temporary and sensitive input values are excluded from diagnostics.

Quick-action tests restrict targets to the modal result area, rather than clicking an identically named control behind the palette. Clipboard tests grant the test browser its clipboard permission, verify the actual serialized clipboard contents and wait for the pasted property before saving. Corner cancellation compares the exact committed pointer-derived baseline, not a rounded coordinate literal. Existing point-editing tests retain their original top-left clockwise anchor assertions; the native geometry implementation must preserve that ordering.

## Build and deployment gates

**Build** validates engine, collaboration and browser jobs, then retains ten reusable package/symbol pairs, a self-hostable server, benchmarks and source/test artifacts. **Desktop** separately compiles Windows, Linux and macOS; compilation is not native interaction certification.

**Pages** deploys the successful main browser artifact without rebuilding it, verifies `build-info.json` against the source commit and runs the **48 static cases** on the public URL. The 13 collaboration cases run against the same browser artifact and a temporary compiled backend during Build. Excluding them from static-only Pages checks is not evidence of a hosted public backend.

A persistent collaboration service requires separate HTTPS hosting and disk. Packages reach NuGet.org only through the configured tagged-release workflow; ordinary CI artifacts are build outputs, not publication.

## Reproduce

```bash
dotnet run --project tests/VectorSpace.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
node --test tests/browser-harness/*.test.mjs
python3 -m unittest discover -s tests/scripts -v
dotnet build server/VectorSpace.Server -c Release
python3 -m unittest discover -s tests/server -v

# A published client must already be served on port 4173.
python3 scripts/run-collaboration-browser-tests.py
# Static checks only:
npm run test:browser -- --grep-invert @collaboration
```

The shape benchmark (`--benchmark-shapes`) verifies native command equivalence for 160 shapes, then compares warm stroke-region rebuilding with retained reuse in interleaved batches. Native allocations, painting, UI, layout, history, network and cold setup are excluded. Existing snapping, appearance, editing-workflow and collaboration benchmarks keep their own correctness oracles. See [performance](PERFORMANCE.md) and [shape semantics](SHAPES.md).

## Certification boundaries

Chromium is the automated browser target. Native UI interaction, other browsers, broad assistive-technology compatibility, production security/load testing, all-filesystem power-loss durability and complete Figma product/pixel parity are not certified. See [features](FEATURES.md), [collaboration](COLLABORATION.md) and [hosting](HOSTING.md).
