# Validation and delivery

## Regression suites

The engine runner contains **421 cases**: 385 prior geometry/layout/design-system/prototype/appearance/tool cases and 36 editing-workflow regressions. New coverage includes typed property ownership, unsupported/locked targets, geometry/content preservation, variable materialization, persistent instance overrides, variant matching, compact JSON defaults, native-schema migration, stable ordering and bounded rename preflight. The stable-order case exhaustively compares selected subsets of sibling lists up to eight items against an independent block-move oracle.

The collaboration model runner contains **20 cases**. The shared-editor runner contains **23**, including a projection oracle with **59 equivalence/reuse assertions**. They cover guarded history, independent edits, replies/insertion, local boundaries and conflict recovery. Six Python publication cases validate version/provenance and asset collection.

**Nine black-box HTTP tests** start the actual ASP.NET process. In addition to authorization, roles, revocation, concurrent edits, idempotence, presence, long polls and restart recovery, they verify schema-4 rooms upgrade once to schema 5 with a durable system revision. Existing historical versions and post-upgrade edits remain usable. No in-memory transport replaces the network service.

Counts describe source coverage, not proof that an arbitrary build passed. Exact-commit Actions results and retained reports are the evidence.

## Browser workflows

The full suite contains **52 Chromium cases**: 40 static editor workflows and 12 real multi-window collaboration workflows tagged `@collaboration`. New cases use actual property-copy shortcuts, selective transfer controls, typography transfer, numbered/capture-based batch naming and stable layer ordering. A two-client case verifies property paste and conditional own undo preserve a peer's later geometry change.

The inline-text regression saves immediately after typing, checks the host save has completed and canvas keyboard focus is restored, reopens the selected text with Enter and cancels only the second transaction. It does not insert a delay before the initial save to hide the asynchronous input problem.

Other browser coverage includes tools and baseline gestures, image/crop/effect/SVG workflows, design-system bindings/variants, prototype playback/authoring, real room creation/joining, sharing consent, following, comments, offline reconnect/recovery, history/restore/revocation and access revoked during active pointer/text transactions.

Tests use real pointer, keyboard, file-picker and clipboard input. `?test=1` provides read-only diagnostics, including save/focus and deferred-delivery state; it does not expose a document mutation API. Room-seeding requests go to a real temporary server, while editing goes through Uno controls. Credentials are temporary and sensitive input values are excluded from diagnostics.

## Build and deployment gates

**Build** validates engine, collaboration and browser jobs and creates ten reusable package/symbol pairs, a self-hostable server, benchmark outputs and source/test artifacts. Successful browser delivery requires every job. **Desktop** independently compiles Windows, Linux and macOS. Compilation does not constitute native interaction certification.

**Pages** deploys the successful main browser artifact, checks `build-info.json` against the source commit, and runs the **40 static cases** on the public URL. The 12 collaboration cases already ran against that artifact with a compiled temporary backend during Build. Their explicit exclusion from a static-only Pages run is not evidence of a public collaboration server.

A persistent public backend requires separate HTTPS hosting and disk. Templates, uploaded server archives and ephemeral tests do not provision that service. Generated NuGet archives are not proof of NuGet.org publication.

## Reproduce

```bash
dotnet run --project tests/VectorSpace.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
python3 -m unittest discover -s tests/scripts -v
dotnet build server/VectorSpace.Server -c Release
python3 -m unittest discover -s tests/server -v

# With a separately served published client on port 4173:
python3 scripts/run-collaboration-browser-tests.py
# Static checks only:
npm run test:browser -- --grep-invert @collaboration
```

The workflow benchmark uses 20,000 siblings with 10,000 selected, comparing a correct stable remove/append reference against stable partition. It checks exact output before reporting timing and temporary allocation. A separate property-capture check compares zero versus 10,000 unrelated descendants. Run with `--benchmark-workflows`; native history, validation, layout, network and painting are excluded. Existing snapping, paths, appearance and collaboration-projection benchmarks retain their own reference/pixel oracles.

## Certification boundaries

Chromium is the automated browser target. Native UI interaction, other browsers, broad assistive-technology compatibility, production security/load testing, all-filesystem power-loss durability and complete Figma product/pixel parity are not certified. See [editing workflows](EDITING_WORKFLOWS.md), [features](FEATURES.md), [collaboration](COLLABORATION.md) and [hosting](HOSTING.md).
