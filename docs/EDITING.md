# Tools and direct editing

Version 0.5 adds a point-editing mode, precision line construction, tool-lifecycle fixes and reusable geometry commands. All editing is C#/Uno/Skia; browser diagnostics are read-only.

## Vector points

Select a vector shape and press **Enter**, double-click it, or choose **Edit vector points**. Rectangles, ellipses, polygons, stars, lines and single-contour SVG paths can be converted transactionally. Existing point paths enter without a conversion history entry. Compound paths are left unchanged and report that multiple contours are not supported; this avoids flattening holes or losing disconnected geometry. Instance geometry must be detached first.

Click an anchor to select it, Shift-click to toggle additional anchors, or drag a box to select points. Dragging a selected anchor moves all selected anchors and their tangent handles from one captured baseline. Arrow keys nudge selected points by one world unit; Shift uses ten. A plain click on an already selected anchor collapses the selection on release; dragging preserves it. Layer coordinates do not change. Point coordinates account for path scaling and rotated/reflected ancestors.

Click a segment to insert an anchor. Cubic insertion uses exact de Casteljau subdivision: both halves retain the original curve. The same gesture can move the inserted anchor, and undo reverts insertion and movement together. **Split selected segments** inserts midpoints where both ends are selected.

Selected anchors expose their handles. Drag a handle to retain the opposite handle's length and alignment, or hold Alt to move it independently. Shift constrains the handle angle to 45-degree increments. **Smooth**, **Mirrored**, and **Corner** construct balanced tangents, equal-length tangents, or remove tangents, respectively. B smooths; Alt+B makes corners. Double-clicking an anchor toggles smooth/corner.

Delete removes selected anchors and reconnects surviving neighbors; it does **not** perform Figma's curve-fitting delete-and-heal. At least two anchors must remain. A closed contour reduced to two anchors becomes open. **Reverse path** swaps order and tangent directions. **Open/Close path** changes closure without throwing away endpoint handles. **Simplify freehand** applies only to open straight-segment polylines, preserving both endpoints. Its iterative error-bounded algorithm has a comparison budget; on adversarial input it retains the original contour instead of blocking or discarding detail.

Enter or Escape leaves point mode while retaining the selected layer. During an active point drag, Escape first restores that drag; another Escape leaves point mode. Ctrl+Z/Redo restore document snapshots and rebind the active contour by identity rather than keeping stale references. Undo or redo during a captured point/layer geometry drag first cancels that interaction and releases capture; later pointer movement cannot reapply its abandoned baseline outside history. The point inspector and context menu expose the operations without requiring shortcuts.

Conversion degree-elevates quadratic segments exactly. Rational conics are approximated with sixteen quadratic pieces per conic before cubic degree elevation. This is not an exact rational representation. Closest-segment insertion uses bounded numerical search in screen coordinates, not a certified global solver for every self-intersecting cubic.

## Drawing tools

All sixteen existing tools are available in the palette and under **Tool: …** quick actions. **Keep drawing tool** retains a shape or pen tool after completion; the default still returns to Move. Tool changes finish an active viable pen contour without resetting the newly chosen tool, and cancel incomplete shape drags.

Shift constrains rectangle/ellipse/frame shapes to equal dimensions. Lines/arrows instead snap their direction to 45-degree increments, retaining their length. Alt creates around the initial center. Canonical path data represents horizontal and vertical lines exactly even though the editor retains a nonzero selection box. Resizing and SVG export respect that path's canonical dimensions.

While drawing, Up/Down changes polygon/star sides. Alt+Up/Down adjusts the star's inner ratio. For rectangles, Up/Down adjusts corner radius; Shift uses a larger step. These changes belong to the same drawing transaction.

Pen shows live anchors, handles and a prospective next segment. Shift constrains the next point; click-drag makes Bézier handles; click the first anchor closes the path. Backspace removes the last uncommitted anchor. Ctrl+Z retains the established behavior of canceling the entire unfinished stroke. Enter finishes. New shapes and pen/pencil strokes choose the topmost unlocked, visible frame using local bounds and ancestor clipping, excluding instance subtrees. New pen/pencil strokes use that frame's local coordinates; drawing inside an auto-layout frame creates an absolute child instead of fighting its allocation during the gesture.

Pencil samples are reduced on completion using the configured **Pencil tolerance** in screen pixels (default 0.65, zero disables). This is polyline simplification, not pressure-sensitive brush simulation. Text still uses the host's text input/IME; it is not a full mixed-run rich-text editor. Saving resolves pending edits so an uncommitted contour is not silently serialized as a half-finished transaction.

## Selection transforms, clipboard and guides

**Shift+H / Shift+V** reflect selected roots around their shared world-space bounds. Rotating a selection by 90 degrees likewise uses a shared pivot. Selected descendants are not transformed twice, and locked roots are excluded. **Ctrl+Alt+arrows** resize selected layers; Shift uses ten units. Only manipulated axes lose hug/fill, matching side-handle semantics. Explicit spacing commands preserve the first item's position and support free-positioned roots; flow children use the parent's auto-layout gap instead.

**Ctrl+Shift+V** pastes VectorSpace clipboard content at its recorded world placement. Normal paste retains its established 24-unit offset. Clipboard variable dependencies remain remapped by the existing document service.

With rulers visible, drag an existing guide to move it. Drop it into the ruler or outside the canvas to remove it. Creation, movement and deletion are undoable. Shift-constrained layer drags now apply snap correction only on the permitted axis; snapping no longer breaks the constraint. Locked layers remain stationary during mixed-selection drags.

## Performance and reuse

`PathEditing`, `CubicSegment`, `DrawingGeometry` and `EditorSession` transform methods are UI-independent in **VectorSpace.Editing**. `EditablePathConversion` belongs to **VectorSpace.Skia**. Point mode and tool lifecycle live in the reusable **VectorSpace.Editor** control; the inspector and command integration live in **VectorSpace.Workbench**.

Point rendering builds native `SKPath` commands directly, avoiding SVG string construction/parsing for every geometry edit. Full-precision SVG path export no longer rounds anchors to two decimals. Geometry keys still invalidate only changed paths; pure translation reuses cached geometry. Point drags capture their baseline once, avoid per-sample document cloning, skip layout for geometric previews and coalesce canvas invalidations. Commit-time history and validation still serialize/traverse documents and are not made constant-time by this work.

Run the scoped benchmark with `dotnet run --project tests/VectorSpace.Tests -c Release -- --benchmark-editing`. It compares 1,000-anchor geometry rebuilds (five batches of sixty), checks equivalent native geometry and records managed allocations. It excludes UI, native allocation, history, hit testing and raster/GPU drawing. Do not interpret it as whole-application FPS.

## Boundaries

This increment improves the existing tool set; it does not implement every Figma vector tool. Vector networks, multi-contour topology editing, shape builder, variable-width strokes, pressure brushes, vector eraser/knife/lasso, general path joining, corner smoothing, full rich text and pixel-identical Figma UI remain outstanding. Arbitrary affine skew, nested auto-layout precedence and other compatibility limits remain documented in [Features](FEATURES.md).

Behavior reference: [Figma vector editing](https://help.figma.com/hc/en-us/articles/360039957634-Edit-vector-layers). This reference defines terminology, not a binary/pixel compatibility claim.
