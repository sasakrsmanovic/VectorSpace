# Corners, arcs, strokes and live Boolean editing

VectorSpace 0.8 adds reusable shape controls, direct canvas grips and retained native geometry. These are actual editable document features shared by the Uno browser/native hosts, rendering, picking, history and collaboration—not decorative inspector fields.

## Explore the shape lab

Save your current file, open **Ctrl+K → Shape playground**, and confirm replacement. The original editable study contains independent corners, a gradient ring sector, a dashed open arc and a live subtraction with its original operands. No raster screenshot or external image substitutes for the shapes.

## Independent corners

Select a rectangle, frame or another supported corner-bearing shape. The **Corner radius** section switches between linked and independent radii. Independent values use a spatial two-by-two arrangement: top-left, top-right, bottom-left, bottom-right. Each rendered radius is clamped to half the shorter edge while the authored value remains unchanged; resizing a shape back up restores the visible authored radius.

**Edit shape on canvas** exposes circular corner grips. Dragging normally changes all corners; **Alt-drag** changes only the grabbed corner. Gesture samples are evaluated from the initial local-space geometry, rather than accumulating rounding error. Rotated/reflected ancestors do not change which corner is edited. **Enter** finishes. **Escape** cancels the active drag and leaves shape mode. Undo during a captured drag cancels the capture first, so subsequent movement cannot reapply abandoned geometry.

The ordinary uniform-radius appearance field remains available. It clears independent radii when deliberately applying a uniform value. Independent geometry editing in a linked instance is disabled: edit the main component or detach before changing its structure. Uniform radius and stroke-style overrides retain their existing instance-history behavior.

Rounded frame clipping, drawing-parent selection and presentation clipping use the same independent-corner geometry. Corner smoothing/squircles and per-edge border widths are not implemented.

## Ellipse arcs and rings

An ellipse has optional `EllipseArc` data: clockwise start and signed sweep in degrees, normalized inner radius and an open flag. Null means the legacy complete ellipse. The inspector offers **Sector**, **Ring** and **Open arc**, with start/sweep and inner-radius percentage fields. Reset restores the complete ellipse without changing its identity.

On-canvas **Start**, **End** and **Inner** grips edit the same values. Moving Start retains the endpoint when the sweep range permits; moving End adjusts sweep. Angles unwrap across the negative/positive boundary, and **Shift** snaps angular changes to fifteen degrees. Inner radius uses the ellipse's normalized axes, so a wide ellipse does not produce a circular-distance error. Coincident endpoints of a full sweep receive visibly separated grips.

Closed sectors and rings use native arc/conic contours with correct winding for holes. A zero sweep or closed inner radius of one produces empty geometry. Open arcs draw only strokes, not an implicitly filled chord; their stroke alignment falls back to centered because they are open contours. Negative sweeps reverse direction. This is not a variable-width arc stroke or a complete rounded-end sector model.

## Stroke geometry

The custom stroke editor adds **Center / Inside / Outside**, **Butt / Round / Square** caps, **Miter / Round / Bevel** joins, a miter limit, explicit dash pattern and dash phase. Existing color, width, opacity and dash presets remain available. An odd-length dash list repeats to form its alternating dash/gap cycle.

Closed inside/outside strokes are constructed as fill regions: a doubled centered stroke is intersected with or subtracted from the source silhouette. Open/mixed-open contours and text retain centered behavior. Rendering and geometric picking share the resulting stroke region, so outside strokes are not picked as though they were centered. Editor picking adds a zoom-aware tolerance around that region. Text remains rendered through the existing text engine rather than being converted implicitly into outlines.

Stroke colors and opacity are applied when drawing, not embedded in the region cache. Color/opacity edits and moving or rotating a whole layer therefore do not rebuild the local stroke outline. Width, cap, join, miter, phase, dashes or source geometry do invalidate it. Native stroke entries are bounded by an LRU (2048 by default), disposed on eviction, and trimmed immediately when the capacity is reduced. The additional hit-tolerance outline is owned by the same entry.

Supported stroke properties survive native JSON, full-node clipboard operations, style transfer and component synchronization. Property clipboard packet version **2** preserves the new stroke semantics; version 1 remains readable and older readers reject packet 2 rather than silently dropping fields. Independent radii themselves remain geometry data rather than a new selective-transfer group.

## Non-destructive Boolean operations

Use Union/Subtract/Intersect/Exclude from quick actions or the editor's Boolean commands. Select 2–128 eligible sibling vector shapes. The new group retains the actual original children, their identifiers, styles and transforms. The lowest selected layer in sibling order is the subtraction base; click selection order does not change it.

Only the combined result is painted. Normal selection does not treat the retained cutout as visible content inside a hole. Enter the group, use the layer tree or deep-select to edit its original operands. Geometry and operand-transform changes rebuild the result; paint-only changes to an operand do not, since the group owns the visible result style. Nested live Boolean groups are supported and cache invalidation follows their geometry dependencies.

The contextual inspector switches operation and provides:

- **Release operands:** restores the retained shapes, paints, identities and world placement through the existing ungroup transaction.
- **Flatten result:** replaces the group with its exact native contour result while retaining its own identifier, style and local coordinate frame. Undo restores the original Boolean group and operands.

Empty results remain valid and reversible. Text, ordinary child-bearing containers and instance structural edits are rejected before mutation. A main component can contain a live Boolean; creating a component from the result wraps the Boolean rather than changing it into a different node kind and losing the operation.

The group's authored coordinate frame is retained while editing/flattening, rather than repeatedly rebasing normalized image/gradient paints. Its dimensions and child resizing currently follow existing group/constraint semantics; this is not Figma's complete automatic Boolean bounds/reflow or arbitrary nonuniform affine scaling behavior. Unclipped operands can extend outside nominal bounds, and callers should choose export bounds that include the intended result. Boolean operations use filled operand silhouettes; outline an open stroke before using its stroke area as an operand.

## Outline strokes

**Outline stroke** creates native filled contour children for each visible nonzero stroke and retains an additional child for the original fill. Each stroke keeps its own color/opacity; root opacity, effects, transformation and identifier remain on the resulting group. Dashed gaps and compound contours are preserved. The operation does not merge differently colored strokes into one solid paint or round-trip the result through SVG.

A supported first-fill/first-stroke variable binding is transferred to the corresponding filled child. Outline is transactional and reversible. Text glyph outlining and arbitrary ordinary container outlining are not included. Native compound contours can be selected, transformed, painted, serialized and exported. Version 0.9 also converts them explicitly into editable cubic contours; see [compound editing](CONTOURS.md) for approximation and topology boundaries.

## Native geometry and SVG

Primitive rendering now creates `SKPath` geometry directly. Native `PathCommand` data represents Move, Line, Quadratic, rational Conic, Cubic and Close, with fill rule and conic weight. This preserves rational curves and compound paths through native flattening, outlining, cloning and shared documents. It is mutually exclusive with the existing editable-anchor list or imported SVG path string. Validation checks sequencing, finite coordinates, positive weights and the 100,000-command limit.

SVG cannot encode general rational conics directly. `ShapePathSvg` converts them adaptively to cubic segments using a **sampled 0.0005 local-unit positional tolerance**, a bounded subdivision depth and output budget. It is not a mathematical global-error proof or a lossless native interchange format. Tests compare high-resolution path length/positions as well as unchanged native commands. Default low-resolution path measurement is intentionally not used to compare a long native conic with many short exported cubic segments.

Use **`SceneSvg.Export`** for live Booleans or aligned strokes. It prepares temporary result copies and exports the same stroke regions without modifying the authored document. The portable two-argument `SvgFormat.Export` API remains available, but explicitly rejects features requiring native geometry instead of emitting a wrong approximation. Centered stroke SVG includes caps, joins, miter limit, dashes/phase and fill rules. Import reads those supported properties with inheritance.

Shape descriptors were introduced in **schema 6**. The current **schema 7** additionally supports editable compound contours and migrates earlier supported schemas. Clients and self-hosted servers should upgrade together. Room journals add a durable schema-upgrade revision without rewriting prior history. Keep backups before upgrading; older clients must reject newer unsupported schemas.

## Reusable components

`VectorSpace.Model` (namespace `VectorSpace.Core`) owns shape descriptors and portable geometry math. `VectorSpace.Documents` owns validation, native persistence and SVG construction. `VectorSpace.Editing.ShapeGesture` captures backend-independent gesture baselines. `VectorSpace.Skia` owns native paths, stroke/Boolean caches, live operations and renderer-aware export. `VectorSpace.Controls` supplies compact stroke/corner/arc editors; `VectorSpace.Editor` supplies canvas grips. The workbench composes them into undoable authoring workflows.

```csharp
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Skia;

var shape = new DesignNode
{
    Kind = NodeKind.Ellipse,
    Width = 200, Height = 140,
    Arc = new EllipseArc(-30, 285, .55),
    Fills = [new() { Color = "#7F56D9" }],
    Strokes = [new()
    {
        Color = "#241342", Width = 8,
        Alignment = StrokeAlignment.Outside,
        Cap = StrokeCap.Round, Join = StrokeJoin.Round,
        Dashes = [12, 6], DashOffset = 3
    }]
};
using var renderer = new SceneRenderer();
renderer.StrokeCacheCapacity = 512;
File.WriteAllBytes("shape.png", renderer.ExportPng([shape], shape.LocalBounds.Inflate(24), 2));
File.WriteAllText("shape.svg", SceneSvg.Export(renderer, [shape], shape.LocalBounds.Inflate(24)));
```

The stroke benchmark (`--benchmark-shapes`) checks exact captured-command equivalence before reporting retained-versus-rebuilt timing/allocation for 160 shapes. Source geometry is retained in both paths. Measurements exclude painting, native allocations, UI, history, networking and cold setup; they are not a whole-application FPS claim. See [performance](PERFORMANCE.md), [validation](VALIDATION.md) and [remaining boundaries](FEATURES.md).

References: [Figma corner radius](https://help.figma.com/hc/en-us/articles/360050986854-Adjust-corner-radius-and-smoothing), [Figma paint/stroke properties](https://developers.figma.com/docs/plugins/api/node-properties/), [Skia paths](https://api.skia.org/classSkPath.html), [Skia path measurement](https://api.skia.org/classSkPathMeasure.html). These guide terminology/geometry, not binary or pixel-parity certification.
