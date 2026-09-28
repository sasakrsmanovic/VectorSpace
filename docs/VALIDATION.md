# Validation and delivery

## Separate suites and evidence

The original editor runner contains **385 cases**, covering geometry, layout, transactions, design systems, prototype state, images/effects, SVG and tool editing. Six Python publication cases cover version/provenance and static asset collection. Exact-commit Actions logs remain the source of truth for passing status; a source count does not certify a green build.

The collaboration model runner adds **20 cases** for projection, independent property merging, conditional history, concurrent insertion/replies, conflict recovery, hierarchy validation and generated wire serialization. The shared editor runner adds **23 cases** for transaction boundaries, rollback, viewer guards, local selection/viewport preservation, history detachment, remote deletion, recovery, version restoration and invitation endpoint handling.

Eight black-box Python cases start the **actual ASP.NET process**. They exercise authorization, invitation revocation, viewer restrictions, concurrent requests, atomic conflicts, idempotent retries, long-poll deltas, ephemeral presence and service restart with an incomplete journal tail. No in-memory transport substitutes for the real server.

Published-browser acceptance contains **46 Chromium cases**: 35 existing editor/design-system/prototype/appearance/tool workflows plus eleven tagged `@collaboration` multi-window workflows. Tests use actual pointer, keyboard, clipboard and file-picker operations on the Uno application. Read-only `?test=1` diagnostics expose control bounds/state, not document-mutation commands. Sensitive invitation inputs omit diagnostic values.

## Multi-user browser acceptance

Separate browser contexts join actual service rooms through the Share dialog. Cases cover synchronized presence and independent edits; own undo preserving a peer's properties; an active pointer transaction deferring remote application; viewer/commenter behavior; disconnected edits and reconnect; conflicting work recovered after reload; participant following; concurrent replies across modal boundaries; room/invitation creation through controls; version download/restore and active-access revocation, including revocation during an active pointer or inline-text edit. The shared-editor runner includes 59 seeded projection equivalence and key/value reuse assertions.

A test harness creates an ephemeral backend, key and room directory, runs Playwright against a separately published/served client, and disposes the backend afterward. API calls seed test rooms/invitations, while document editing and workbench workflows use real controls. Test credentials refer only to the disposable test service.

## Build and deployment gates

**Build** has engine, collaboration and browser jobs. Successful browser artifacts require all three. It publishes ten reusable package/symbol pairs and a self-hostable server, preserves source snapshots and records benchmark/test artifacts. **Desktop** independently compiles Windows, Linux and macOS; compilation is not full native interaction certification.

**Pages** deploys the successful main-branch browser artifact, verifies its source commit through `build-info.json` and executes the **35 static editor cases** against the public URL. The eleven collaboration cases already ran against the same artifact and an actual temporary backend in Build. They are explicitly excluded from the static-only public check; that does not constitute live public-backend verification.

A public collaboration server must be deployed separately with HTTPS and persistent storage. A hosting blueprint, uploaded server artifact or passing CI service does not mean such a deployment exists. Generated NuGet archives are not evidence of publication to NuGet.org.

## Reproduce

```bash
dotnet run --project tests/VectorSpace.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
python3 -m unittest discover -s tests/scripts -v
dotnet build server/VectorSpace.Server -c Release
python3 -m unittest discover -s tests/server -v

# With the published client already served on port 4173:
python3 scripts/run-collaboration-browser-tests.py
# Static-only checks against a configured URL:
npm run test:browser -- --grep-invert @collaboration
```

The collaborator benchmark verifies a one-property batch reconstructs the exact native document and reports both wire bytes and projection/diff CPU/allocation cost. Existing snapping, appearance and direct-path benchmarks retain their own oracle/pixel checks. No whole-application FPS, constant-time projection or arbitrary-scale collaboration claim is made.

## Remaining certification boundaries

Chromium is the browser acceptance target. Native UI interaction, other browser engines, broad accessibility, production security/load testing, power-loss durability on every filesystem, distributed storage and complete Figma feature/pixel compatibility are not certified by these checks. See [features](FEATURES.md), [collaboration semantics](COLLABORATION.md) and [hosting](HOSTING.md).
