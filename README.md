# VectorSpace

**A local-first vector design, prototyping and collaborative editing workspace built with [Uno Platform](https://platform.uno) and SkiaSharp.**

[![Build](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml)
[![MIT license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Model.svg?label=NuGet)](https://www.nuget.org/packages/VectorSpace.Model)
[![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Model.svg)](https://www.nuget.org/packages/VectorSpace.Model)

An infinite canvas, floating tool palette, contextual properties, local design systems, isolated prototype playback and a reusable SkiaSharp engine. The browser host runs C# through Uno WebAssembly—not an HTML mockup, WebView wrapper or embedded third-party editor. Windows, macOS, Linux and browser hosts share the workbench.

![VectorSpace browser workbench](docs/images/workbench.png)

**Source version: 0.9.0-alpha.1.** Original code, icons and samples. Independent of Figma and not a claim of complete Figma feature or pixel parity. Successful Pages runs and public `build-info.json` identify the deployed source; this version label alone does not prove deployment.

**[Open the browser editor](https://wieslawsoltes.github.io/VectorSpace/)** · **[Editing workflows](docs/EDITING_WORKFLOWS.md)** · **[Collaboration](docs/COLLABORATION.md)** · **[Hosting](docs/HOSTING.md)** · **[Compatibility](docs/FEATURES.md)**

## Compound vector editing

Edit holes and disjoint contours as one vector layer: select anchors across contours, subdivide exact cubic segments, adjust Bézier handles, cut/join endpoints, navigate or remove contours, and choose non-zero/even-odd fill rules. A compact custom Uno vector inspector composes the reusable topology/editing APIs. Sparse pointer captures update only selected anchors instead of resetting the whole path each sample.

[Compound editing workflows, APIs and limits](docs/CONTOURS.md) explain schema **7**, conic conversion tolerance, shared-property conflicts and scoped performance measurements. This is compound cubic editing, not full branching vector-network or Figma pixel parity.

## Shape editing and native geometry

Independent corners, ellipse sectors/rings/open arcs, aligned strokes, caps/joins/miter/dash phase and live Boolean groups are editable through custom contextual controls. Corner/arc grips operate directly on the canvas with modifier, undo and cancellation support. Flattening and stroke outlines preserve exact native conic/compound contours rather than round-tripping the document through SVG text.

The stroke/Boolean caches share geometry between drawing and picking, retain unchanged resources and dispose evicted paths. Open **Ctrl+K → Shape playground** for the original editable study. [Shape workflows, APIs and boundaries](docs/SHAPES.md) describe the supported behavior; this is not full vector-network editing or pixel-certified Figma parity. Current native schema **7** requires updating collaborative clients and the self-hosted server together.

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

## NuGet packages

All ten libraries are MIT-licensed, versioned together with the app and published to [NuGet.org](https://www.nuget.org/packages?q=VectorSpace) on tagged releases, with symbol packages (`.snupkg`) and SourceLink. The seven engine packages target `net10.0` and have no UI dependency (only `VectorSpace.Skia` needs SkiaSharp); `VectorSpace.Controls`, `VectorSpace.Editor` and `VectorSpace.Workbench` are Uno Platform libraries targeting `net10.0-desktop` and `net10.0-browserwasm`. The core model package is `VectorSpace.Model` because the `VectorSpace.Core` ID on NuGet.org belongs to another owner; its assembly and namespaces remain `VectorSpace.Core`. The separate ASP.NET collaboration host lives under `server/` and is not a package.

```bash
dotnet add package VectorSpace.Model --prerelease
```

| Package | Version | Downloads | Description |
|---|---|---|---|
| [VectorSpace.Model](https://www.nuget.org/packages/VectorSpace.Model) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Model.svg)](https://www.nuget.org/packages/VectorSpace.Model) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Model.svg)](https://www.nuget.org/packages/VectorSpace.Model) | Scene graph, typed styles, variables, prototype reactions and affine/path geometry (assembly `VectorSpace.Core`). |
| [VectorSpace.Layout](https://www.nuget.org/packages/VectorSpace.Layout) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Layout.svg)](https://www.nuget.org/packages/VectorSpace.Layout) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Layout.svg)](https://www.nuget.org/packages/VectorSpace.Layout) | Flow/grid auto-layout, constraints and indexed snapping. |
| [VectorSpace.Documents](https://www.nuget.org/packages/VectorSpace.Documents) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Documents.svg)](https://www.nuget.org/packages/VectorSpace.Documents) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Documents.svg)](https://www.nuget.org/packages/VectorSpace.Documents) | Versioned native JSON, validation, safe SVG interchange, property clipboard, samples and storage contracts. |
| [VectorSpace.Editing](https://www.nuget.org/packages/VectorSpace.Editing) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Editing.svg)](https://www.nuget.org/packages/VectorSpace.Editing) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Editing.svg)](https://www.nuget.org/packages/VectorSpace.Editing) | Selection, transactions, undo/redo, property transfer, batch naming, components and viewport. |
| [VectorSpace.Prototyping](https://www.nuget.org/packages/VectorSpace.Prototyping) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Prototyping.svg)](https://www.nuget.org/packages/VectorSpace.Prototyping) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Prototyping.svg)](https://www.nuget.org/packages/VectorSpace.Prototyping) | Deterministic, isolated prototype playback, navigation, overlays and interpolation. |
| [VectorSpace.Collaboration](https://www.nuget.org/packages/VectorSpace.Collaboration) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Collaboration.svg)](https://www.nuget.org/packages/VectorSpace.Collaboration) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Collaboration.svg)](https://www.nuget.org/packages/VectorSpace.Collaboration) | Identity-addressed transactions, conditional undo, replicas, presence and HTTP room transport. |
| [VectorSpace.Skia](https://www.nuget.org/packages/VectorSpace.Skia) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Skia.svg)](https://www.nuget.org/packages/VectorSpace.Skia) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Skia.svg)](https://www.nuget.org/packages/VectorSpace.Skia) | SkiaSharp design/prototype rendering, hit testing, Boolean paths and PNG export. |
| [VectorSpace.Controls](https://www.nuget.org/packages/VectorSpace.Controls) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Controls.svg)](https://www.nuget.org/packages/VectorSpace.Controls) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Controls.svg)](https://www.nuget.org/packages/VectorSpace.Controls) | Compact Uno editor controls, vector icons, numeric scrubbing, color input, rename/property panels and avatars. |
| [VectorSpace.Editor](https://www.nuget.org/packages/VectorSpace.Editor) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Editor.svg)](https://www.nuget.org/packages/VectorSpace.Editor) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Editor.svg)](https://www.nuget.org/packages/VectorSpace.Editor) | Embeddable Uno/Skia design surface, prototype player and remote-presence overlay. |
| [VectorSpace.Workbench](https://www.nuget.org/packages/VectorSpace.Workbench) | [![NuGet](https://img.shields.io/nuget/vpre/VectorSpace.Workbench.svg)](https://www.nuget.org/packages/VectorSpace.Workbench) | [![Downloads](https://img.shields.io/nuget/dt/VectorSpace.Workbench.svg)](https://www.nuget.org/packages/VectorSpace.Workbench) | Complete authoring workbench: layers, inspector, tools, clipboard, sharing, recovery and history. |

Dependencies follow the project references: `Layout` and `Documents` → `Model`; `Editing` → `Layout` + `Documents`; `Prototyping` → `Editing`; `Collaboration` → `Documents`; `Skia` → `Prototyping` + SkiaSharp; `Controls` is standalone Uno; `Editor` → `Controls` + `Skia`; `Workbench` → `Editor` + `Collaboration`.

### VectorSpace.Model

The serializable document model: pages, nodes, fills/strokes/effects, auto-layout settings, components, variables and prototype reactions, plus double-precision `Vec2`/`RectD`/`Matrix2D` geometry. Use it on its own to generate or inspect designs in any .NET app. No dependencies and no UI.

```bash
dotnet add package VectorSpace.Model --prerelease
```

**Key types** (namespace `VectorSpace.Core`)

- `DesignDocument` – pages, comments, variable collections; `AllNodes()`, `Find(id)`, `RebuildParents()`.
- `DesignPage` / `DesignNode` – node tree with `Kind`, bounds, `Fills`, `Strokes`, `Shadows`, `Layout`, `Children`, `Add(child)`.
- `DesignNode.WorldMatrix` / `WorldBounds` – parent-relative transforms resolved to canvas space.
- `FillStyle`, `StrokeStyle`, `ShadowStyle`, `AutoLayout` – typed appearance and layout settings.
- `Vec2`, `RectD`, `Matrix2D` – immutable geometry with `Map`, `Inverse`, `Union` and friends.

**Usage**

```csharp
using VectorSpace.Core;

var document = new DesignDocument { Name = "Landing page" };
var frame = new DesignNode { Kind = NodeKind.Frame, Name = "Hero", Width = 1280, Height = 720, Fill = "#FFFFFF" };
document.Pages[0].Nodes.Add(frame);

var button = frame.Add(new DesignNode { Name = "CTA", X = 80, Y = 520, Width = 200, Height = 56, CornerRadius = 12 });
button.Fills[0] = new FillStyle { Kind = FillKind.LinearGradient };
button.Strokes.Add(new StrokeStyle { Color = "#1E1E1E", Width = 2 });
button.Rotation = 15;

RectD world = button.WorldBounds;            // rotated, in canvas coordinates
Vec2 local = button.WorldMatrix.Inverse.Map(world.Center);
Console.WriteLine($"{document.AllNodes().Count()} nodes, CTA at {world}");
```

### VectorSpace.Layout

Horizontal/vertical/grid auto-layout with wrap, padding, gaps, hug/fill sizing and edge/scale constraints, plus an indexed snap engine for gesture alignment. Depends on `VectorSpace.Model`; no UI.

```bash
dotnet add package VectorSpace.Layout --prerelease
```

**Key types**

- `LayoutEngine.Arrange(node)` / `Arrange(roots)` – lays out auto-layout frames recursively.
- `LayoutEngine.Resize(node, width, height)` – resizes and applies child constraints.
- `SnapIndex` – prebuilt, sorted snap targets (bounds and guides) with `Snap(moving, tolerance)`.
- `SnapEngine.Snap(...)` – one-shot snapping; `SnapResult` holds the `Correction` and `SnapLine`s to draw.

**Usage**

```csharp
using VectorSpace.Core;
using VectorSpace.Layout;

var row = new DesignNode { Kind = NodeKind.Frame, Name = "Toolbar" };
row.Layout = new AutoLayout { Direction = LayoutDirection.Horizontal, Gap = 8, HugWidth = true, HugHeight = true };
for (var i = 0; i < 3; i++) row.Add(new DesignNode { Name = $"Item {i}", Width = 40, Height = 40 });

LayoutEngine.Arrange(row);                    // positions children, hugs the frame
Console.WriteLine($"{row.Width} x {row.Height}");

var index = new SnapIndex(row.Children.Select(c => c.WorldBounds));
SnapResult snap = index.Snap(new RectD(62, 18, 40, 40), tolerance: 4);
Console.WriteLine($"Correction {snap.Correction}, {snap.Lines.Count} guide lines");
```

### VectorSpace.Documents

Reading, writing and validating native `.vectorspace` JSON (schema 7, migrating 1–6), safe SVG import/export (no scripts, external resources or DTDs), the style-only property clipboard, sample documents and the storage interfaces hosts implement. Depends on `VectorSpace.Model`; no UI.

```bash
dotnet add package VectorSpace.Documents --prerelease
```

**Key types**

- `DocumentJson` – `Load`, `Save`, `Validate`, `CloneNode`, `SaveNodes`/`LoadNodes`.
- `SvgFormat` – `Export(roots, bounds)` and `Import(svg)` returning `SvgImportResult` (document + warnings).
- `PropertyClipboard` – portable fill/stroke/effect/typography transfer packets.
- `IWorkspaceStorage` / `ISharedRecoveryStorage` – autosave, open/save and recovery contracts for hosts.
- `SampleDocument`, `PrototypeSample`, `AppearanceSample` – original editable samples.

**Usage**

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;

DesignDocument document = DocumentJson.Load(File.ReadAllText("design.vectorspace")); // validates and migrates
File.WriteAllText("copy.vectorspace", DocumentJson.Save(document));

var frame = document.Pages[0].Nodes[0];
File.WriteAllText("frame.svg", SvgFormat.Export([frame], frame.WorldBounds));

SvgImportResult imported = SvgFormat.Import(File.ReadAllText("icon.svg"));
foreach (var warning in imported.Warnings) Console.WriteLine(warning);

string clipboard = PropertyClipboard.Copy(frame, PropertyGroups.Fills | PropertyGroups.Strokes);
PropertyClipboardPacket packet = PropertyClipboard.Read(clipboard);
```

### VectorSpace.Editing

The headless editor: selection, transactional edits with undo/redo, moving/grouping/aligning, clipboard, components and variants, property transfer, batch rename and a viewport. Build your own UI or automation on it. Depends on `Model`, `Layout` and `Documents`; no UI.

```bash
dotnet add package VectorSpace.Editing --prerelease
```

**Key types**

- `EditorSession` – `Document`, `Page`, `Selection`, `Tool`, `Edit(label, action)`, `Undo()`/`Redo()`, `Changed` event.
- `EditorSession.MoveSelection`, `GroupSelection`, `Align`, `CopySelection`/`Paste` – common commands.
- `ComponentService` – `MakeComponent`, `InsertInstance`, `Synchronize(document)`.
- `PropertyTransfer.Paste` and `LayerRename.Plan`/`Apply` – property paste and previewed batch rename.
- `Viewport` – zoom/pan with `WorldToScreen`, `ScreenToWorld`, `ZoomAt`, `Fit`.

**Usage**

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

var session = new EditorSession(SampleDocument.Create());
session.Changed += (_, e) => Console.WriteLine($"{e.Kind}: {e.Label}");

var card = new DesignNode { Name = "Card", Width = 320, Height = 200, CornerRadius = 16 };
session.Edit("Add card", () => session.AddNode(card));   // one undoable transaction
session.Select(card);
session.MoveSelection(24, 0);
session.UpdateSelection("Tint", node => node.Fill = "#0D99FF");

Console.WriteLine($"{session.UndoLabel} (dirty: {session.IsDirty})");
session.Undo();
```

### VectorSpace.Prototyping

A private, deterministic prototype runtime: it plays a copy of the document (the source is never mutated), evaluates triggers, conditions and runtime variables, and drives navigation, Back, overlays, scrolling and transitions from an explicit clock. Depends on `VectorSpace.Editing`; no UI.

```bash
dotnet add package VectorSpace.Prototyping --prerelease
```

**Key types**

- `PrototypeSession` – `View`, `Dispatch(trigger, hitId)`, `DispatchKey`, `Back()`, `ScrollBy`, `AdvanceTo(ms)`.
- `PrototypeView` – current frame, scroll and overlay stack (`InputRoot` receives input).
- `PrototypeAnimation` / `PrototypeTween` – eased transitions and smart-animate interpolation.
- `PrototypeGeometry` – overlay placement and scroll clamping.

**Usage**

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Prototyping;

var player = new PrototypeSession(PrototypeSample.Create());
Console.WriteLine($"Starting frame: {player.View.Frame.Name}");

var hit = player.View.InputRoot.Children.FirstOrDefault();
if (player.Dispatch(PrototypeTrigger.Click, hit?.Id, userInitiated: true))
{
    player.AdvanceTo(player.ClockMilliseconds + 300);        // deterministic clock drives transitions
    Console.WriteLine($"Now on {player.View.Frame.Name}, animating: {player.IsAnimating}");
}
if (player.CanGoBack) player.Back();
```

### VectorSpace.Collaboration

Client-side collaboration: projects documents into identity-addressed, versioned property cells, diffs and rebases them, keeps guarded shared undo, and talks to a `VectorSpace.Server` room over HTTPS with presence and invitations. Depends on `VectorSpace.Documents`; no UI. See [Collaboration](docs/COLLABORATION.md) for semantics.

```bash
dotnet add package VectorSpace.Collaboration --prerelease
```

**Key types**

- `DocumentProjection` – `FromDocument`, `ToDocument`, `Diff`, `Apply` over `SharedSnapshot` cells.
- `SharedReplica` – confirmed/visible state, pending batches, conditional undo/redo and recovery copies.
- `CollaborationConnection` – `JoinAsync`, `Start`, `Submit`, `Undo`/`Redo`, `SetPresence`, `Participants`.
- `RoomTransport` / `RoomAddress` – room creation, invitations, history and invitation-link parsing.

**Usage**

```csharp
using VectorSpace.Collaboration;
using VectorSpace.Documents;

// Pure projection: documents become versioned property cells that can be diffed.
var before = DocumentProjection.FromDocument(SampleDocument.Create());
var edited = DocumentProjection.ToDocument(before);
edited.Pages[0].Nodes[0].Name = "Renamed";
List<CellChange> changes = DocumentProjection.Diff(before, DocumentProjection.FromDocument(edited));

// Live room (the dispatcher marshals callbacks onto your UI thread).
var address = RoomAddress.Parse(invitationLink);
using var room = await CollaborationConnection.JoinAsync(address, action => { action(); return Task.CompletedTask; });
room.Changed += remote => Console.WriteLine($"{room.Status} ({room.Participants.Count} online)");
room.Start();
if (room.CanEdit) room.Submit(DocumentJson.Save(edited), "Rename frame");
```

### VectorSpace.Skia

The SkiaSharp renderer shared by the editor and headless tools: design and prototype drawing with retained geometry/gradient/effect/image caches, hit testing, Boolean path operations, editable path conversion and PNG export. Depends on `Prototyping`/`Editing` and SkiaSharp; no Uno dependency.

```bash
dotnet add package VectorSpace.Skia --prerelease
```

**Key types**

- `SceneRenderer` – `Draw(canvas, nodes)`, `HitTest(roots, point)`, `Geometry(node)`, `ExportPng(nodes, bounds, scale)`.
- `PrototypeSceneRenderer` – draws and hit-tests a `PrototypeSession`, including overlays.
- `BooleanOperations.Apply(editor, renderer, BooleanOperation.Union)` – Boolean path editing.
- `RasterImageCodec` / `ImageAssetCache` – bounded image import, decode and caching.

**Usage**

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Skia;

var document = DocumentJson.Load(File.ReadAllText("design.vectorspace"));
var frame = document.Pages[0].Nodes[0];
using var renderer = new SceneRenderer();
File.WriteAllBytes("frame.png", renderer.ExportPng([frame], frame.WorldBounds, 2));

DesignNode? hit = renderer.HitTest(document.Pages[0].Nodes, new Vec2(120, 80), deep: true);
using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(800, 600));
renderer.Draw(surface.Canvas, document.Pages[0].Nodes);
```

### VectorSpace.Controls

Dense, accessible Uno Platform controls with shared design tokens: icon buttons with original vector glyphs, scrubbable numeric fields, color fields with a spectrum picker, inspector sections, layer rows, rename and property-transfer panels and participant avatars. It has no document or editor dependency; requires Uno Platform (Skia renderer).

```bash
dotnet add package VectorSpace.Controls --prerelease
```

**Key types**

- `Studio` – tokens (`Accent`, `Ink`, `Font`) and helpers such as `Brush`, `Text`, `Input`, `Columns`.
- `NumericField` – labelled numeric input with scrubbing, `Minimum`/`Maximum`/`Step` and `ValueCommitted`.
- `ColorField` / `ColorSpectrum` – hex color input and Skia spectrum picker.
- `IconButton`, `InspectorSection`, `LayerRow`, `ParticipantStrip`, `BatchRenamePanel`.

**Usage**

```csharp
using Microsoft.UI.Xaml.Controls;
using VectorSpace.Controls;

var size = new InspectorSection("Size");
size.Body.Children.Add(Studio.Columns(
    (new NumericField("W", 320, w => Console.WriteLine($"Width {w}")) { Minimum = 1 }, -1),
    (new NumericField("H", 200, h => Console.WriteLine($"Height {h}")) { Minimum = 1 }, -1)));
size.Body.Children.Add(new ColorField("#0D99FF", hex => Console.WriteLine(hex)));

var panel = new StackPanel { Width = 260 };
panel.Children.Add(new IconButton("undo", "Undo", () => { }));
panel.Children.Add(size);
window.Content = panel;
```

### VectorSpace.Editor

The embeddable design surface: an Uno `SKCanvasElement`-based canvas with direct manipulation, vector and text tools, snapping, image crop, comments, a full-screen `PrototypePlayer` and a retained remote-presence overlay. Bind it to an `EditorSession` and add your own chrome. Depends on `VectorSpace.Controls` and `VectorSpace.Skia`; requires Uno Platform.

```bash
dotnet add package VectorSpace.Editor --prerelease
```

**Key types**

- `DesignSurface` – `Session`, `Renderer`, `Fit()`, `Invalidate()`, `Present()`, vector-edit commands, `StatusChanged`.
- `PrototypePlayer` – standalone prototype playback control over a `PrototypeSession` (`Playback`).
- `RemotePresenceLayer` / `RemotePeer` – collaborator cursors and selections drawn above the canvas.
- `Keyboard` – current modifier-key state (`Control`, `Shift`, `Alt`) shared with the workbench.

**Usage**

```csharp
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Editor;

var session = new EditorSession(SampleDocument.Create());
var surface = new DesignSurface { Session = session };
surface.StatusChanged += message => Console.WriteLine(message);
session.Tool = EditorTool.Rectangle;
window.Content = surface;

// Or play a prototype without the editor:
window.Content = new PrototypePlayer(PrototypeSample.Create());
```

### VectorSpace.Workbench

The complete VectorSpace authoring experience as one control: toolbar and tool palette, layers, inspector, design systems, prototype editing, clipboard, sharing and collaboration, recovery and history. The host supplies an `EditorSession` and an `IWorkspaceStorage` (browser, desktop or your own). Depends on `VectorSpace.Editor` and `VectorSpace.Collaboration`; requires Uno Platform.

```bash
dotnet add package VectorSpace.Workbench --prerelease
```

**Key types**

- `StudioWorkbench(EditorSession session, IWorkspaceStorage storage)` – the workbench `UserControl`.
- `StudioWorkbench.Surface` – the hosted `DesignSurface` (renderer, typeface, focus).
- `StudioWorkbench.ShowStatus(message, error)` and `OpenCollaborationInvitation(link)`.
- `IWorkspaceStorage` / `ISharedRecoveryStorage` – implement for autosave, open/save and shared recovery.

**Usage**

```csharp
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;

var session = new EditorSession(SampleDocument.Create());
// Supply IWorkspaceStorage; optionally implement ISharedRecoveryStorage.
var workbench = new StudioWorkbench(session, storage);
window.Content = workbench;
window.Closed += (_, _) => workbench.Dispose();
window.Activate();
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

Current native **schema 7** migrates schemas 1–6 and preserves editable compound contours, native conics and instance overrides. SVG/PNG are not lossless native substitutes. Servers upgrade older supported room snapshots after replay by appending a durable system revision; prior history and receipts remain intact. Back up room data and upgrade clients with the service. [Compound geometry and migration](docs/CONTOURS.md#collaboration-and-compatibility).

**Build** gates browser artifacts on engines, shared-editor boundaries, actual HTTP/restart tests and browser acceptance. It preserves benchmarks/server/packages/source and test artifacts. **Desktop** compiles Windows/Linux/macOS. **Pages** deploys the successful main artifact, verifies provenance and tests the public static client; collaboration cases use that same artifact with a real ephemeral backend during Build. **Release** runs for `v*` tags or a supplied manual version: it repeats the engine, server and browser gates, publishes self-contained single-file desktop executables for Windows, macOS and Linux (x64 and arm64), the browser/server/source archives and versioned packages with symbols, and emits `SHA256SUMS.txt`. Tags attach all assets to a GitHub Release and publish the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC, no stored API key) from the protected `nuget` environment. Manual runs are dry runs that upload every asset as workflow artifacts and publish nothing.

See [architecture](docs/ARCHITECTURE.md), [validation](docs/VALIDATION.md), [security](SECURITY.md), [contributing](CONTRIBUTING.md), and [changelog](CHANGELOG.md).

## License and references

MIT for original source, icons and samples. Dependencies retain their licenses; Inter is SIL OFL 1.1. Figma is a trademark of its owner, referenced only for requested behavior. Native `.fig`, remote published libraries, plugins, mixed rich text, vector networks and full Figma product parity remain unfinished.

Technical references: [Uno SDK](https://platform.uno/docs/articles/features/using-the-uno-sdk.html), [SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html), [SkiaSharp](https://github.com/mono/SkiaSharp), [Figma multiplayer](https://www.figma.com/blog/how-figmas-multiplayer-technology-works/).
