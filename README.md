# VectorSpace

**A local-first vector design editor built with Uno Platform and SkiaSharp.**

[![Build](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/VectorSpace/actions/workflows/pages.yml)
[![MIT license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

VectorSpace brings a compact, UI3-style design workspace to a real Uno application: an infinite canvas, floating tool palette, layers and assets, contextual property editing, and a SkiaSharp scene engine. The browser host runs C# in WebAssembly; the editor is **not an HTML mockup or an embedded third-party editor**.

![VectorSpace running in the browser](docs/images/workbench.png)

**Status: 0.2.0-alpha.1.** Independent implementation with original code, icons, and sample artwork. Not affiliated with Figma; no `.fig` import or claim of complete Figma feature/pixel parity. See the [feature boundary](docs/FEATURES.md).

## Browser and desktop

**Browser deployment:** [wieslawsoltes.github.io/VectorSpace](https://wieslawsoltes.github.io/VectorSpace/). A successful **Pages** workflow is the deployment source of truth. The publishing source is **GitHub Actions**, and `build-info.json` identifies the deployed commit.

Windows, macOS, Linux and browser hosts share the same workbench and canvas. Browser recovery data is stored in IndexedDB; desktop recovery files use the local application-data directory. **Save a local copy** downloads an editable `.vectorspace` document. Browser storage is not a backup service.

## Working features

- **Vector editing:** rectangles, rounded rectangles, ellipses, lines, arrows, polygons, stars, frames, sections, slices, cubic pen paths, freehand paths, editable text, and Boolean path operations.
- **Canvas interaction:** selection/deep selection, marquee, move, eight resize handles, rotation, shift constraints, alt-drag duplication, snapping, guides, rulers, grid, zoom-to-cursor, pan, touch gestures, outlines, and inline text editing.
- **Document workflows:** pages, searchable/recyclable layers, rename, visibility, locking, grouping, stacking, alignment/distribution, clipboard, transactional undo/redo, validated JSON persistence, safe SVG interchange and PNG export.
- **Design properties:** layered fills, linear/radial gradients, strokes/dashes, opacity/blends, corner radius, drop shadows, typography, horizontal/vertical wrapping, grid tracks/spans, min/max-constrained fill, baseline alignment, hug sizing, absolute children, and edge/scale constraints.
- **Reusable content and review:** local component sets and variants, linked instances with stable descendant identities, text/fill overrides, typed local variables, aliases and inherited modes, detach/reset, local comments, and clickable frame-to-frame prototypes.

**Design systems:** use **Local variables** in the main menu or quick actions; bind properties and choose modes in the Variables inspector. Use **Add variant** on a component or **Combine as variants** on sibling components, then insert from Assets and switch instance properties. See the [design-system guide](docs/DESIGN_SYSTEMS.md).

**Interaction performance:** gesture-scoped indexed snapping, retained geometry/text caches, conservative viewport culling, unchanged-instance synchronization skips, and selection-only layer-list updates. See [reproducible measurements and limitations](docs/PERFORMANCE.md).

The original **Aether** sample contains editable desktop and mobile landing pages, buttons, and a card component. It is not a background screenshot.

## Pinned toolchain

| Dependency | Version |
|---|---:|
| .NET SDK | 10.0.401 |
| Uno SDK / templates | 6.7.30 |
| Uno WinUI, matched by SDK | 6.7.135 |
| SkiaSharp, matched to Uno's native runtime | 3.119.2 |
| Playwright | 1.63.0 |

Uno versions were resolved from stable NuGet packages on September 27, 2026. Keep managed and native Skia versions aligned; the independently newest SkiaSharp release is not a safe automatic substitute for Uno's matched dependency graph.

## Build and run

Install the SDK in `global.json`, Python 3, and Node.js 22+ for browser tests.

```bash
python3 scripts/fetch-assets.py

# Native Uno Skia host, without installing the browser workload
dotnet run --project src/VectorSpace.App -f net10.0-desktop \
  -p:VectorSpaceDesktopOnly=true

# WebAssembly development host
dotnet workload install wasm-tools
dotnet run --project src/VectorSpace.App -f net10.0-browserwasm

# Engine regression runner; exits nonzero on failure
dotnet run --project tests/VectorSpace.Tests -c Release
```

The asset script fetches Inter from Google Fonts under SIL OFL 1.1 and retains its license. Font binaries are not committed.

### Static browser publication

```bash
dotnet publish src/VectorSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/VectorSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Use HTTP/HTTPS, not `file://`. For a root-domain deployment use `WasmShellWebAppBasePath=/`. The local server reproduces the `/VectorSpace/` Pages base path.

```bash
npm ci
npx playwright install chromium
npm run test:browser
```

Browser tests drive real pointer and keyboard events. `?test=1` exposes read-only diagnostics for assertions, not a document-mutation API. Screenshots, traces, build outputs and package archives are retained by Actions. See [validation](docs/VALIDATION.md) for coverage and limits.

## Reusable libraries

| Package | Responsibility | UI dependency |
|---|---|---|
| `VectorSpace.Core` | Scene graph, typed variables/modes, document types, affine geometry, paths | None |
| `VectorSpace.Layout` | Wrap/grid auto-layout, constraints, indexed snapping | None |
| `VectorSpace.Documents` | JSON validation/source generation, SVG, storage contract | None |
| `VectorSpace.Editing` | Selection, viewport, history, variables, component variants/synchronization | None |
| `VectorSpace.Skia` | Rendering, hit testing, Boolean paths, PNG | SkiaSharp |
| `VectorSpace.Controls` | Icons, dense primitives, numeric scrubbing, color picker | Uno / Skia |
| `VectorSpace.Editor` | Embeddable direct-manipulation surface | Uno / Skia |
| `VectorSpace.Workbench` | Layers, inspector, palette, document/review workflows | Uno / Skia |

All eight libraries are packable. `.nupkg`/`.snupkg` artifacts do **not** imply publication to NuGet.org. Public registry publication requires separate credentials and policy.

```csharp
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Workbench;

var session = new EditorSession(SampleDocument.Create());
// Supply your own implementation of IWorkspaceStorage.
window.Content = new StudioWorkbench(session, storage);
```

Use the engine without Uno:

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
| Save / open / quick actions | Ctrl S / Ctrl O / Ctrl K |
| Hide panels / cancel / rename | Tab / Escape / F2 |

Native text inputs retain their own editing shortcuts. Browser-reserved keys and OS conventions may vary. The in-app help has additional commands.

## Build, deployment and releases

**Build** validates the engine, publishes the browser app, runs browser tests and packs libraries. **Desktop** independently compiles Windows, Linux and macOS hosts. **Pages** deploys only successful main-branch Build artifacts, then tests the public site. **Release** verifies tagged snapshots and publishes browser/source/package archives with SHA-256 checksums. An artifact upload is not a live deployment.

See [architecture](docs/ARCHITECTURE.md), [feature boundaries](docs/FEATURES.md), [contributing](CONTRIBUTING.md), [security](SECURITY.md), and [changelog](CHANGELOG.md).

## License and references

MIT for VectorSpace source, original icons and sample content. Dependencies retain their own licenses; Inter is SIL OFL 1.1. Figma is a trademark of its respective owner and is mentioned only as the requested design/interaction reference.

Technical references: [Uno SDK](https://platform.uno/docs/articles/features/using-the-uno-sdk.html), [SKCanvasElement](https://platform.uno/docs/articles/controls/SKCanvasElement.html), [SkiaSharp](https://github.com/mono/SkiaSharp), [Figma toolbar documentation](https://help.figma.com/hc/en-us/articles/360041064174-Access-design-tools-from-the-toolbar).
