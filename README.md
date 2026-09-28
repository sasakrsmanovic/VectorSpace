# VectorSpace

**A local-first vector design, prototyping and collaborative editing workspace built with Uno Platform and SkiaSharp.**

[![Build](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml)
[![MIT license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

VectorSpace combines an infinite canvas, floating tool palette, contextual properties, local design systems, isolated prototype playback and a reusable SkiaSharp engine. Its browser host runs C# through Uno WebAssembly—not an HTML mockup, WebView wrapper or embedded third-party editor. Windows, macOS, Linux and browser hosts share the same workbench.

![VectorSpace browser workbench](docs/images/workbench.png)

**Source version: 0.6.0-alpha.1.** Original code, icons and samples. Independent of Figma and not a claim of complete Figma feature or pixel parity. The successful Pages workflow and public `build-info.json`, not this source-version label, identify the deployed build.

**[Open the browser editor](https://wieslawsoltes.github.io/VectorSpace/)** · **[Collaboration](docs/COLLABORATION.md)** · **[Host a server](docs/HOSTING.md)** · **[Tools](docs/EDITING.md)** · **[Compatibility boundaries](docs/FEATURES.md)**

## Work locally or together

Local work needs no account. Browser recovery uses IndexedDB; desktop recovery uses the application-data directory. Save downloads a real editable `.vectorspace` document. Browser storage is not a backup service.

Collaboration adds shared property/hierarchy edits, remote cursors and selections, participant following, synchronized comments, role-based invitations, revocation and persistent server revisions. Each participant's undo is guarded against later peer edits. Disconnected edits retry in the same window; rejected or unacknowledged work has separate local recovery copies rather than being silently overwritten.

**GitHub Pages hosts the client only.** A separately running HTTPS collaboration service with persistent disk is required for cross-device sharing. There is no automatically provisioned public server, account subscription or hidden document upload. The repository includes the backend, Docker/Compose and an optional hosting blueprint. [Read the operating model and limits](docs/COLLABORATION.md).

### Share a file

Start your server, open **Share**, enter your name and create a shared file using its creation key. Confirm the upload and save the owner link privately. Then create a separate **Viewer**, **Commenter** or **Editor** invitation for each collaborator. Opening an invitation asks for endpoint confirmation before connecting.

This is capability-link access, not SSO or verified account identity. Anyone holding a link receives its permissions. Tokens are not automatically stored and do not appear in URL queries or recovery journals. Manage invitations to revoke access; previously downloaded copies cannot be revoked.

## Design and edit

| Area | Working behavior |
|---|---|
| Vector tools | Primitive shapes, frames/sections/slices, cubic pen and freehand paths, exact curve subdivision, anchor/handle editing, conversion, guides and Boolean operations. |
| Direct manipulation | Scoped/deep/marquee selection, eight anchored resize handles, rotation, flips, modifier gestures, duplication, snapping, cursor-anchored zoom, pan and inline text editing. |
| Layout | Horizontal/vertical flow, wrapping, grid tracks/spans, padding/gaps, constrained fill, axis-preserving hug/fill resize, absolute children and edge/scale constraints. |
| Appearance | Layered solid/gradient/image paints, Fill/Fit/Crop/Tile, direct image cropping, strokes/dashes, opacity/blends, independent drop/inner shadows and layer blur. |
| Design systems | Local component sets/variants, linked instances, supported overrides, stable descendants, typed variables, aliases, inherited modes and dependency-aware clipboard operations. |
| Prototypes | Isolated private runtime, named flows, triggers/actions/conditions, navigation and Back, modal overlays, frame scrolling, runtime variables, interactive variants and supported transitions. |

Use **Ctrl+K** to open the original **Prototype playground** or **Appearance playground**. Every sample shape, paint and interaction is editable. [Prototyping](docs/PROTOTYPING.md), [appearance](docs/APPEARANCE.md), [design systems](docs/DESIGN_SYSTEMS.md) and [direct editing](docs/EDITING.md) document exact behavior rather than implying unsupported parity.

## Performance by subsystem

The engine uses gesture-scoped indexed snapping, direct native path construction, retained geometry/text resources, bounded image/gradient/effect caches, conservative culling, incremental component synchronization and prepared prototype interpolation. Collaboration transmits changed properties, coalesces ephemeral presence and keeps cursor painting separate from document rendering.

These are specific optimizations, not an application-wide FPS claim. Document projection, layout/validation, canonicalization and snapshot recovery still have workload-dependent costs. CI retains reproducible timing/allocation/wire-size records and verifies equivalent output. [Measurements and limits](docs/PERFORMANCE.md).

## Pinned toolchain

| Dependency | Version |
|---|---:|
| .NET SDK | 10.0.401 |
| Uno SDK | 6.7.30 |
| Uno WinUI, SDK-matched | 6.7.135 |
| Managed/native SkiaSharp | 3.119.2 |
| Playwright | 1.63.0 |

Keep managed and native Skia versions aligned with Uno. The independently newest native Skia package is not a safe drop-in replacement.

## Build and run

Install the SDK in `global.json`, Python 3 and Node.js 22+ for browser testing.

```bash
python3 scripts/fetch-assets.py

# Native Uno Skia host
dotnet run --project src/VectorSpace.App -f net10.0-desktop \
  -p:VectorSpaceDesktopOnly=true

# WebAssembly development host
dotnet workload install wasm-tools
dotnet run --project src/VectorSpace.App -f net10.0-browserwasm

# Engines and shared-editor integration
dotnet run --project tests/VectorSpace.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
```

The asset script fetches Inter under SIL OFL 1.1 and retains its license. Font binaries are not committed.

### Publish the browser client

```bash
dotnet publish src/VectorSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VectorSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Use HTTP/HTTPS, not `file://`. For a root-domain deployment set `WasmShellWebAppBasePath=/`. The local server reproduces the Pages `/VectorSpace/` base path.

### Test browser interactions

With the published client served in another terminal:

```bash
npm ci
npx playwright install chromium

# Static editor workflows only
npm run test:browser -- --grep-invert @collaboration

# All workflows, including independent browser clients and a real temporary backend
dotnet build server/VectorSpace.Server -c Release
python3 scripts/run-collaboration-browser-tests.py
```

The tests use real pointer, keyboard, clipboard and file-picker interactions. `?test=1` exposes read-only state and control bounds, not a mutation API. Sensitive invitation inputs are excluded from diagnostic values. The backend test runner generates temporary credentials and destroys its test room directory when finished.

### Run a persistent collaboration server

```bash
export VECTORSPACE_CREATE_KEY="$(openssl rand -hex 32)"
export VECTORSPACE_DATA="$HOME/.local/share/VectorSpace/rooms"
dotnet run --project server/VectorSpace.Server -c Release \
  --urls http://127.0.0.1:5097

# Alternative, with a named persistent Docker volume:
docker compose up --build -d
```

The default Compose port is loopback-only. Use a maintained HTTPS proxy and explicit allowed origins for Internet use. Do not commit keys, delete persistent volumes unintentionally, or run several server processes against one data directory. [Hosting and backups](docs/HOSTING.md).

## Ten reusable libraries

| Package | Responsibility | UI dependency |
|---|---|---|
| `VectorSpace.Core` | Scene graph, variables, reactions, affine geometry and paths | None |
| `VectorSpace.Layout` | Flow/grid layout, constraints and indexed snapping | None |
| `VectorSpace.Documents` | Native JSON, validation, SVG, samples and storage contracts | None |
| `VectorSpace.Editing` | Selection, transactions, local/shared history boundary and components | None |
| `VectorSpace.Prototyping` | Deterministic private playback, navigation and interpolation | None |
| `VectorSpace.Collaboration` | Identity-addressed transactions, conditional undo, replicas and HTTP lifecycle | None |
| `VectorSpace.Skia` | Design/prototype rendering, picking, Boolean paths and PNG | SkiaSharp |
| `VectorSpace.Controls` | Dense controls, icons, numeric scrubbing, color picker and participant avatars | Uno / Skia |
| `VectorSpace.Editor` | Design surface, prototype player and retained remote-presence overlay | Uno / Skia |
| `VectorSpace.Workbench` | Complete authoring, sharing, recovery and history workflows | Uno / Skia |

All ten libraries are packable. Generated `.nupkg` and `.snupkg` files do not imply publication to NuGet.org. The ASP.NET host lives separately under `server/` and is not a UI package.

```csharp
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;

var session = new EditorSession(SampleDocument.Create());
// Inject your IWorkspaceStorage implementation; optionally add ISharedRecoveryStorage.
window.Content = new StudioWorkbench(session, storage);
```

The engine can be used without Uno:

```csharp
using VectorSpace.Documents;
using VectorSpace.Skia;

var document = DocumentJson.Load(File.ReadAllText("design.vectorspace"));
var frame = document.Pages[0].Nodes[0];
using var renderer = new SceneRenderer();
File.WriteAllBytes("frame.png", renderer.ExportPng([frame], frame.WorldBounds, 2));
```

## Keyboard essentials

| Action | Shortcut |
|---|---|
| Move / Frame / Rectangle / Ellipse | V / F / R / O |
| Pen / Pencil / Text / Comment | P / Shift P / T / C |
| Pan / cursor-anchored zoom | Space-drag / Ctrl-wheel |
| Multi-select / deep-select | Shift-click / Ctrl-click |
| Duplicate while dragging | Alt-drag |
| Undo / redo | Ctrl Z / Ctrl Shift Z |
| Group / ungroup / auto-layout | Ctrl G / Ctrl Shift G / Shift A |
| Nudge / large nudge | Arrow / Shift-arrow |
| Fit all / selected | Shift 1 / Shift 2 |
| Place image | Ctrl Shift K |
| Save / open / quick actions | Ctrl S / Ctrl O / Ctrl K |
| Hide panels / cancel / rename | Tab / Escape / F2 |

Native text inputs retain their own editing shortcuts. Browser-reserved keys and OS conventions vary. Low-level text entry, focus, scrolling, menus and dialogs still use Uno primitives; every Figma screen and accessibility workflow is not pixel-certified.

## Persistence, CI and releases

Native schema **4** migrates schemas 1–3. SVG/PNG are interchange/rendering outputs, not lossless native substitutes. Shared revisions use a separate server journal without changing the native document schema.

**Build** gates the browser artifact on engine, collaboration, editor-boundary, actual HTTP/restart and real browser tests, and produces benchmark/server/package/source artifacts. **Desktop** compiles Windows/Linux/macOS. **Pages** deploys the successful main-branch artifact, checks provenance and tests the public static editor. Multi-user acceptance uses the same artifact and a real temporary backend during Build; Pages itself cannot host that backend. **Release** handles explicitly tagged source/browser/package releases.

See [architecture](docs/ARCHITECTURE.md), [validation](docs/VALIDATION.md), [feature boundaries](docs/FEATURES.md), [security](SECURITY.md), [contributing](CONTRIBUTING.md) and [changelog](CHANGELOG.md).

## License and references

MIT for original source, icons and samples. Dependencies retain their own licenses; Inter is SIL OFL 1.1. Figma is a trademark of its owner and is referenced only for requested interaction behavior. Native `.fig`, remote published libraries, plugin execution, mixed rich text, vector networks and complete Figma product parity remain unfinished.

Technical references: [Uno SDK](https://platform.uno/docs/articles/features/using-the-uno-sdk.html), [SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html), [SkiaSharp](https://github.com/mono/SkiaSharp), [Figma multiplayer architecture](https://www.figma.com/blog/how-figmas-multiplayer-technology-works/).
