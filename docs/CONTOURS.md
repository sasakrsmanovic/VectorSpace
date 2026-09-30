# Compound vector editing

VectorSpace 0.9 extends the point editor from one contour to **multiple open or closed cubic contours in one layer**. Holes and disconnected islands remain separate during rendering, picking, subdivision, export and editing. This is not a branching vector-network editor or a per-region paint model.

## Editing

Save the current document and open **Ctrl+K → Vector playground** for original editable hole/island, paired-cubic and open-endpoint studies. The examples are actual document geometry, not screenshots.

Select an imported compound SVG path or a native path and press **Enter**, double-click it, or choose **Edit vector points**. Polygon, line, quadratic and cubic segments retain their geometry. Native rational conics are converted only on this explicit command using the shared bounded, sampled 0.0005 local-unit cubic approximation; undo restores the original exact native commands. There is no claim of lossless rational-conic editing after conversion.

Layer selection itself does not begin a move transaction or normalize geometry. Drag capture, document-history snapshots and snapping baselines are deferred until the pointer crosses the movement threshold; a remote document replacement before activation cancels the pending gesture.

Click an anchor to select it; Shift-click adds or removes anchors, including anchors in different contours. Drag selected anchors together, drag their Bézier handles, or use arrow keys (Shift for ten-unit nudges). A plain click collapses a selection only on release so dragging an already selected anchor retains the other selected anchors. Escape/Undo during a captured drag cancels it before any further pointer movement can mutate the restored document. The layer's authored frame, identity and paint remain unchanged rather than rebasing all contours after each edit. When initially converting a zero/subpixel line or arrow, the editable layout frame is normalized to its legal nonzero size; local coordinates and world placement are preserved, rotation/reflection fields are retained, and the new scaling basis participates in subsequent resizing. Undo restores the original primitive and its zero extent.

The reusable **VectorEditingControl** provides compact selection/tangent/subdivision controls, previous/next contour navigation, active-contour selection, closure, cut/join, reverse/delete contour and **Non-zero / Even-odd** fill rules. Those rules change actual fill geometry, not just the preview overlay. Under non-zero winding, reversing an inner contour can form or remove a hole. Even-odd uses crossing parity regardless of orientation.

**Subdivide** processes only real segments with both endpoints selected. The bulk API builds each affected contour once instead of reconstructing topology for each inserted edge. An open endpoint has no outgoing segment; a closed seam connects only to that contour's own first anchor. No segment is manufactured between disconnected contours. Deleting selected anchors is preflighted across every affected contour; each must retain at least two anchors. Use **Delete contour** to remove an entire contour; keep one contour or delete the layer outside point editing.

**Cut at selected anchor** (`X` in point mode) opens a closed contour at that anchor, duplicating the seam endpoint so the complete curve is retained. Cutting an interior anchor of an open contour creates two contours with independent copies of the cut endpoint. Cutting an already-open endpoint is rejected. **Join selected endpoints** (`Ctrl+J` in point mode) reverses endpoint orientation as necessary and adds a straight connector; it does not silently weld coincident endpoints or move authored points. Joining the two ends of one open contour closes it (at least three anchors required). These commands use normal local/shared document history.

## Model and APIs

`DesignNode.Contours` holds `PathContour` objects, each with `Points` and `Closed`. It is mutually exclusive with legacy `Points`, native `Commands` and imported `PathData`. The legacy single-contour form remains supported. Cut/join/remove operations normalize back to that form when only one contour remains. Native schema **7** prevents older clients from silently ignoring compound geometry. Existing schemas 1–6 remain readable. Coordinates now serialize only authored `x` and `y`: derived `length` and `isFinite` metadata are omitted from new documents, history and shared geometry. Older files containing those redundant fields still load; lengths and finiteness are recomputed from the validated coordinates.

`PathTopology` is a retained, non-owning view: anchor indices flatten contours in document order, while segment identifiers are their **global start anchor indices**, not dense edge ordinals. `Next`/`Previous` return -1 at open endpoints. Rebuild after structural edits, same-count list reordering, closure changes or document replacement. Position/handle changes remain visible through retained references.

```csharp
using VectorSpace.Core;
using VectorSpace.Editing;

var vector = new DesignNode
{
    Kind = NodeKind.Path, Width = 200, Height = 100,
    PathWidth = 200, PathHeight = 100,
    Contours =
    [
        new() { Points = [new() { Position = new(0, 0) }, new() { Position = new(80, 50) }] },
        new() { Points = [new() { Position = new(120, 20) }, new() { Position = new(200, 80) }] }
    ]
};
var topology = new PathTopology(vector);
var drag = new PathPointDrag(vector, topology, [0, 2]);
drag.Apply(new(12, 8)); // World delta, evaluated from the captured baseline.
drag.Apply(new(20, 8)); // Does not accumulate the preceding sample.
// The host wraps the gesture in EditorSession.Begin/Preview/CommitInteraction.
// Drop the capture after cancellation/history replacement; create a fresh topology
// after ContourEditing.Cut, Join, Remove, Reverse or structural PathEditing edits.
```

`ContourEditing` owns pure cut/join/reverse/remove/closure operations. `PathEditing` owns exact cubic subdivision, tangent changes, selected-anchor movement and deletion. `NativeShapeGeometry`, `VectorPath` and `SceneSvg` keep contour boundaries and fill rules through the existing native/SVG pipelines. Geometry caches compare contour boundaries, closure and anchor/handle values without building a fresh serialized key on each warm query. Native Booleans/outlines clear the alternate editable representation atomically.

Limits: 100,000 aggregate anchors per path and 10,000 contours; compound contours require two or more anchors. Invalid coordinates, mixed representations and over-budget edits reject before document mutation. A singleton imported contour is rejected rather than silently disappearing. Simplification remains bounded Ramer–Douglas–Peucker for straight open freehand contours only, evaluated independently for each contour.

## Performance scope

Point dragging reapplies only the selected anchors/handles in **O(k)** per sample after retaining topology, rather than resetting all n anchors. Capture also validates the retained contour lists and deduplicates/sorts the selection; it is not an O(k) guarantee for the entire gesture setup. `PathPointDrag.Apply` allocates no managed objects in the regression workload. Document-history snapshots, layout, hit testing and renderer key scans retain their own costs; this does not make the entire editor O(k).

`--benchmark-contours` compares the prior whole-path snapshot/reset strategy against sparse capture for 10,000 anchors across 2,500 contours, with two selected anchors and 60 samples per interaction. It verifies 610,000 full anchor-state comparisons before five interleaved warmed batches. Its timings exclude topology construction, history, renderer scans, painting, UI, networking and cold startup. Reported results belong to the exact Actions commit; no whole-editor FPS or Figma speed comparison is implied.

## Collaboration and compatibility

Compound geometry is one guarded `contours` property in shared transactions. A peer's independent transform can coexist and survive conditional own undo. Simultaneous edits to that same property are not a vector CRDT: conflicts use existing recovery behavior. Upgrade clients and the self-hosted server together and back up room data before schema 7 migration. Room upgrades are durable revisions; older history remains readable.

Not included: arbitrary branching vector networks, per-region paints, paint-bucket/shape-builder tools, knife cuts at arbitrary intersections, weld/auto-heal algorithms, pressure strokes, general path morphing, structural instance editing or automatic Figma-style bounds rebasing. Mixed/open contours retain centered-stroke fallback for aligned strokes. Prototypes conservatively crossfade changed compound geometry rather than claiming shape morphing.

References: [Figma vector editing](https://help.figma.com/hc/en-us/articles/360039957634-Edit-vector-layers) and [Figma vector-network structure](https://developers.figma.com/docs/plugins/api/VectorNetwork/). These guide terminology and explicitly distinguish compound contours from the broader vector-network model.
