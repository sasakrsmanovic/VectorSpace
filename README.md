# VectorSpace

**A local-first vector design, prototyping and collaborative editing workspace built with Uno Platform and SkiaSharp.**

[![Build](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml)
[![MIT license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

An infinite canvas, floating tool palette, contextual properties, local design systems, isolated prototype playback and a reusable SkiaSharp engine. The browser host runs C# through Uno WebAssembly—not an HTML mockup, WebView wrapper or embedded third-party editor. Windows, macOS, Linux and browser hosts share the workbench.

![VectorSpace browser workbench](docs/images/workbench.png)

**Source version: 0.7.0-alpha.1.** Original code, icons and samples. Independent of Figma and not a claim of complete Figma feature or pixel parity. Successful Pages runs and public `build-info.json` identify the deployed source; this version label alone does not prove deployment.

**[Open the browser editor](https://wieslawsoltes.github.io/VectorSpace/)** · **[Editing workflows](docs/EDITING_WORKFLOWS.md)** · **[Collaboration](docs/COLLABORATION.md)** · **[Hosting](docs/HOSTING.md)** · **[Compatibility](docs/FEATURES.md)**

## New editing workflows

Copy and paste supported properties using **Ctrl+Alt+C/V**, or choose just fills, strokes, effects, typography and appearance in the dedicated property panel. Position, dimensions, content, identity and prototype connections stay intact. Supported instance styles and names persist through synchronization and structurally matched variant swaps.

Batch rename through **F2**, **Ctrl+R** or quick actions, with a before/after preview, literal/linear-time regex matching, capture groups and ascending/descending padded numbers. Stable Bring/Send ordering preserves relative layer order and avoids repeated list shifts. Save now completes its host operation and restores authoring focus so Enter can reliably reopen the selected inline text editor.

[Read the exact property, naming, instance and schema-5 behavior](docs/EDITING_WORKFLOWS.md). This is whole-layer typography, not mixed rich text; property transfer does not copy every Figma property or import a foreign design library.

## Design, edit and present

| Area | Working behavior |
|---|---|
| Vector tools | Shapes, frames/sections/slices, cubic pen/freehand paths, exact subdivision, multi-anchor/handle editing, conversion, guides and Boolean operations. |
| Direct manipulation | Scoped/deep/marquee selection, anchored resize handles, rotation/flips, modifiers, duplication, snapping, zoom/pan, inline text, stable layer order and guarded naming. |
| Layout | Horizontal/vertical flow, wrap, grid tracks/spans, padding/gaps, constrained fill, axis-preserving hug/fill resize, absolute children and edge/scale constraints. |
| Appearance | Layered solid/gradient/image paints, Fill/Fit/Crop/Tile, direct image crop, strokes/dashes, opacity/blends, independent drop/inner shadows and layer blur. |
| Design systems | Local components/variants, linked instances, supported style/content/name overrides, stable descendants, typed variables/aliases/modes and dependency-aware clipboard. |
| Prototypes | Isolated private runtime, named flows, triggers/actions/conditions, navigation/Back, overlays, frame scrolling, runtime variables, interactive variants and supported transitions. |
| Collaboration | Shared properties/hierarchy, guarded own undo, presence/following, comments, revocable role invitations and durable revision download/restore against a separate service. |

Use **Ctrl+K → Prototype playground** or **Appearance playground** to explore original editable samples. [Direct tools](docs/EDITING.md), [appearance](docs/APPEARANCE.md), [design systems](docs/DESIGN_SYSTEMS.md) and [prototyping](docs/PROTOTYPING.md) describe their exact behavior and limits.

## Local work and sharing

Local work needs no account. Browser recovery uses IndexedDB; desktop recovery uses the application-data directory. Save downloads an editable `.vectorspace` file. Local storage is not a backup service.

**GitHub Pages hosts the client only.** Cross-device sharing requires a separately operated HTTPS collaboration service with persistent disk. There is no automatic public backend, subscription or hidden upload. The repository includes ASP.NET source, Docker/Compose configuration and an optional hosting blueprint.

Start your service, open **Share**, confirm the upload and save the owner link privately. Create separate Viewer, Commenter or Editor invitations. The server enforces permissions; invitation possession is the authorization model, not SSO or verified identity. Tokens are not automatically stored, included in URL queries or written into recovery journals. Revocation cannot erase previously downloaded copies.

Disconnected edits retry within the same window. Rejected and unacknowledged work has separate local recovery copies; recovery after reload requires review rather than blind replay over later peer edits. Shared undo uses guarded properties, not whole-document rollback. [Collaboration semantics and operational bounds](docs/COLLABORATION.md).

## Performance by subsystem

Indexed gesture snapping, direct native path construction, retained geometry/text resources, bounded image/gradient/effect caches, conservative culling, incremental component synchronization and prepared prototype interpolation reduce specific costs. Property capture copies only the source layer's mutable styles and shares immutable strings; it does not traverse a subtree. Extreme layer ordering uses an O(n) stable partition. Collaboration transmits changed properties and coalesces ephemeral presence separately from document painting.

CI retains equivalent-output benchmarks for snapping, paths, appearance, shared projection and editing workflows. These are workload measurements, not whole-editor FPS claims. History, validation, layout, projection and scene materialization still have document-dependent costs. [Measurements and limits](docs/PERFORMANCE.md).

## Pinned toolchain

| Dependency | Version |
|---|---:|
| .NET SDK | 10.0.401 |
| Uno SDK | 6.7.30 |
| Uno WinUI, SDK-matched | 6.7.135 |
| Managed/native SkiaSharp | 3.119.2 |
| Playwright | 1.63.0 |

Keep managed and native Skia aligned with Uno. An independently newer native package is not a safe drop-in replacement.

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

# Engine and shared-editor regressions
dotnet run --project tests/VectorSpace.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.Tests -c Release
dotnet run --project tests/VectorSpace.Collaboration.EditorTests -c Release
```

The asset script fetches Inter under SIL OFL 1.1 and retains its license. Font binaries are not committed.

### Publish and test the browser client

```bash
dotnet publish src/VectorSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VectorSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Use HTTP/HTTPS, not `file://`. Use `WasmShellWebAppBasePath=/` for a root-domain deployment. With the published site served in another terminal:

```bash
npm ci
npx playwright install chromium
npm run test:browser -- --grep-invert @collaboration

# All workflows, including separate browser profiles and a real temporary server:
dotnet build server/VectorSpace.Server -c Release
python3 scripts/run-collaboration-browser-tests.py
```

Tests use real pointer, keyboard, clipboard and file-picker input. `?test=1` exposes read-only state/control bounds, not mutation commands. Credentials are excluded from diagnostic values. The collaboration runner generates temporary credentials and destroys its room directory afterward.

### Run a persistent collaboration service

```bash
export VECTORSPACE_CREATE_KEY="$(openssl rand -hex 32)"
export VECTORSPACE_DATA="$HOME/.local/share/VectorSpace/rooms"
dotnet run --project server/VectorSpace.Server -c Release \
  --urls http://127.0.0.1:5097

# Or, with a named persistent Docker volume:
docker compose up --build -d
```

The Compose port is loopback-only. Use HTTPS and exact allowed origins for Internet hosting. Do not commit keys, remove volumes unintentionally or run multiple processes against one data directory. [Deployment, upgrades and backups](docs/HOSTING.md).

## Download

Every [release](https://github.com/wieslawsoltes/VectorSpace/releases/latest) ships a self-contained, single-file desktop app — no .NET install needed:

| OS | x64 | Arm64 |
| --- | --- | --- |
| Windows | `VectorSpace-<version>-win-x64.zip` | `VectorSpace-<version>-win-arm64.zip` |
| macOS | `VectorSpace-<version>-osx-x64.tar.gz` | `VectorSpace-<version>-osx-arm64.tar.gz` |
| Linux | `VectorSpace-<version>-linux-x64.tar.gz` | `VectorSpace-<version>-linux-arm64.tar.gz` |

Extract and run `VectorSpace` (`VectorSpace.exe` on Windows). Builds are not code-signed yet: on macOS clear the quarantine flag with `xattr -d com.apple.quarantine VectorSpace`; on Windows choose **More info → Run anyway** in SmartScreen. Verify downloads against `SHA256SUMS.txt`.

The libraries below are published to [NuGet.org](https://www.nuget.org/packages?q=VectorSpace), e.g. `dotnet add package VectorSpace.Workbench`.

## Ten reusable libraries

| Package | Responsibility | UI dependency |
|---|---|---|
| `VectorSpace.Model` (assembly `VectorSpace.Core`) | Scene graph, typed styles, variables, reactions and affine/path geometry | None |
| `VectorSpace.Layout` | Flow/grid layout, constraints and indexed snapping | None |
| `VectorSpace.Documents` | Native/property JSON, validation, SVG, samples and storage contracts | None |
| `VectorSpace.Editing` | Selection, transactions, property transfer, batch naming, components and history | None |
| `VectorSpace.Prototyping` | Private playback, navigation and interpolation | None |
| `VectorSpace.Collaboration` | Identity-addressed transactions, conditional undo, replicas and HTTP lifecycle | None |
| `VectorSpace.Skia` | Design/prototype rendering, picking, Boolean paths and PNG | SkiaSharp |
| `VectorSpace.Controls` | Dense controls, icons, numeric/color input, property/rename panels and avatars | Uno / Skia |
| `VectorSpace.Editor` | Design surface, prototype player and retained remote-presence overlay | Uno / Skia |
| `VectorSpace.Workbench` | Authoring, clipboard, sharing, recovery and history workflows | Uno / Skia |

All ten libraries are packed with symbols and published to NuGet.org on tagged releases. The core model package is `VectorSpace.Model` because the `VectorSpace.Core` ID on NuGet.org belongs to another owner; its assembly and namespaces remain `VectorSpace.Core`. The separate ASP.NET host lives under `server/`.

```csharp
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;

var session = new EditorSession(SampleDocument.Create());
// Supply IWorkspaceStorage; optionally implement ISharedRecoveryStorage.
window.Content = new StudioWorkbench(session, storage);
```

The engine works without Uno:

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
| Copy / paste properties | Ctrl Alt C / Ctrl Alt V |
| Rename selected layers | F2 / Ctrl R |
| Nudge / large nudge | Arrow / Shift-arrow |
| Fit all / selected | Shift 1 / Shift 2 |
| Place image | Ctrl Shift K |
| Save / open / quick actions | Ctrl S / Ctrl O / Ctrl K |
| Parent / sibling navigation | Shift Enter / Tab |

Native text inputs retain their editing shortcuts. Browser-reserved keys and OS conventions vary; quick actions and F2 provide alternatives. Low-level text, focus, scrolling, menus, checkboxes and dialogs still use Uno primitives. Broad accessibility and every Figma screen are not pixel-certified.

## Persistence and delivery

Current native **schema 5** migrates schemas 1–4 and preserves new instance style/name records. SVG/PNG are not lossless native substitutes. A 0.7 server upgrades a schema-4 room after replay by appending a system revision; existing history and receipts remain intact. Back up server data and upgrade clients with the service. [Migration details](docs/EDITING_WORKFLOWS.md#native-format-and-shared-room-upgrade).

**Build** gates browser artifacts on engines, shared-editor boundaries, actual HTTP/restart tests and browser acceptance. It preserves benchmarks/server/packages/source and test artifacts. **Desktop** compiles Windows/Linux/macOS. **Pages** deploys the successful main artifact, verifies provenance and tests the public static client; collaboration cases use that same artifact with a real ephemeral backend during Build. **Release** runs for `v*` tags or a supplied manual version: it repeats the engine, server and browser gates, publishes self-contained single-file desktop executables for Windows, macOS and Linux (x64 and arm64), the browser/server/source archives and versioned packages with symbols, and emits `SHA256SUMS.txt`. Tags attach all assets to a GitHub Release and publish the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC, no stored API key) from the protected `nuget` environment. Manual runs are dry runs that upload every asset as workflow artifacts and publish nothing.

See [architecture](docs/ARCHITECTURE.md), [validation](docs/VALIDATION.md), [security](SECURITY.md), [contributing](CONTRIBUTING.md), and [changelog](CHANGELOG.md).

## License and references

MIT for original source, icons and samples. Dependencies retain their licenses; Inter is SIL OFL 1.1. Figma is a trademark of its owner, referenced only for requested behavior. Native `.fig`, remote published libraries, plugins, mixed rich text, vector networks and full Figma product parity remain unfinished.

Technical references: [Uno SDK](https://platform.uno/docs/articles/features/using-the-uno-sdk.html), [SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html), [SkiaSharp](https://github.com/mono/SkiaSharp), [Figma multiplayer](https://www.figma.com/blog/how-figmas-multiplayer-technology-works/).
