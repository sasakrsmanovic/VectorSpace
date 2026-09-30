# Validation and delivery

## Engine and protocol coverage

The engine runner retains the earlier geometry, layout, design-system, prototype and editing regressions and adds compound vector coverage. It checks contour-local adjacency and subdivision, winding and holes, rational-conic conversion, cut/join/reverse/delete preflight, clipboard/components, schema migration, native cache invalidation and sparse captured dragging. Pixel comparisons and high-resolution curve samples supplement object-state assertions. The executable prints the exact case count and exits nonzero on any failure. The source contains 608 engine cases, including 16 conversion/resize regressions for zero/subpixel lines and arrows under rotation/reflection, coordinate-only serialization, legacy derived-metadata compatibility for schemas 1–7 and finite-coordinate validation.

The collaboration model runner contains 20 cases. The shared-editor runner contains 24, including 59 projection equivalence/reuse assertions and a compound-property-versus-peer-transform case. Six publication tests and nine input-readiness tests cover package/assets metadata, actual Skia control readiness and modal containment.

Eleven black-box HTTP tests start the actual ASP.NET process. They cover authorization, revocation, concurrency, idempotence, presence, long polls, journal recovery and persisted schema-4/5/6 room upgrades to current schema 7. The schema-6 case authors compound contours after upgrade, restarts the service and checks earlier history. An in-memory mock does not replace the server.

Source counts describe coverage, not a passing delivery. Use the matching commit's Actions run and retained reports for results.

## Browser acceptance

The source suite contains **72 Chromium cases**: **58 static editor cases** and **14 independent multi-window collaboration cases** tagged `@collaboration`. The eight compound static cases cover Enter conversion, holes/islands, cross-contour anchor selection and dragging, contour deletion/undo, fill rules, cut/join, capture cancellation, seam subdivision and the original Vector playground. Two further cases convert zero-extent lines/arrows through Enter, check world endpoints, keyboard-resize the resulting path and undo back to the exact primitive. Prior shape, text, image, prototype, design-system, property-clipboard and auto-layout workflows remain in the suite.

The new compound multi-window case changes inner anchors in one client, moves the layer in another, then verifies own undo and redo preserve the peer transform. Whole `contours` properties use the existing guarded shared transactions; these tests do not establish vector-CRDT semantics for simultaneous edits to the same property. Existing role/revocation/reconnect/recovery/presence/history/comment tests are retained.

All edits use actual pointer, keyboard, file-picker, clipboard and Uno controls. `?test=1` exposes read-only state and bounds, never a document mutation API. Tests inspect downloaded native files and real rendered pixels. Modal result containment prevents clicking identically named inspector controls behind a dialog. Clipboard operations wait for actual copied/pasted contents. Input retries and skipped failing cases are not used to obtain a passing report.

## Build and deployment gates

**Build** validates engine, collaboration and browser jobs, retaining ten reusable package/symbol pairs, a self-hostable server, benchmarks and source/test artifacts. **Desktop** separately compiles Windows, Linux and macOS. Compilation is not native interaction certification.

**Pages** deploys the successful main browser artifact without rebuilding it, verifies `build-info.json` against its source commit, then runs the **58 static cases** on the public URL. The 14 collaboration cases run against that browser artifact and a temporary real backend during Build. Static Pages success is not proof of a persistent public backend deployment.

A production collaboration service needs separate HTTPS hosting and persistent disk. NuGet.org publication requires the configured tagged-release workflow; normal CI packages are build artifacts, not a release publication.

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

The sparse capture benchmark (`--benchmark-contours`) validates 610,000 anchor states before comparing full-path capture/reset against selected-anchor capture. Topology is retained in both paths. It excludes history, renderer scans, painting, layout, hit testing, UI, networking and cold setup. Other geometry/appearance/snapping/editing/collaboration benchmarks retain their own equivalence oracles. See [performance](PERFORMANCE.md), [compound editing](CONTOURS.md) and [shape semantics](SHAPES.md).

## Certification boundaries

Chromium is the automated browser target. Other browsers, native UI interaction, broad assistive-technology compatibility, production security/load testing, all-filesystem power-loss durability, complete branching vector networks and Figma product/pixel parity are not certified. See [features](FEATURES.md), [collaboration](COLLABORATION.md) and [hosting](HOSTING.md).
