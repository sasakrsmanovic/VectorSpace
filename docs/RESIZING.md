# Constrained direct manipulation

## Handles and sizing modes

Side handles modify one dimension; the untouched dimension retains its existing fixed, hug or fill behavior. A horizontal resize of a wrapping frame can therefore reflow its children and grow or shrink its hug-height dimension. Corner handles explicitly size both dimensions. Holding Shift preserves the original aspect ratio, including when shrinking from a side handle. Alt resizes around the center; Alt+Shift combines centered and proportional resizing.

Min/max limits are applied before locating the stationary anchor. Dragging a left/top edge into the minimum no longer shifts the opposite edge. Centered resizes keep the center, including when clamped. Dimensions stop at the minimum instead of flipping when the pointer crosses the opposite edge. If the explicit limits cannot accommodate the original aspect ratio, limits take precedence; the ratio cannot be preserved in that contradictory case.

`ResizeGeometry.Calculate` is a pure allocation-free helper. It returns local bounds, the normalized stationary anchor, and manipulated-axis flags. `GestureGeometry.ResizeFromHandle` restores the transaction baseline, applies sizes through the layout engine, and locates the anchor using the original linear transform. This works with rotation, reflections and nested rigid transforms. It positions using the *arranged* dimensions, so a reflow on the unmodified hug axis cannot introduce position drift.

```csharp
var baseline = DocumentJson.CloneNode(node);
var initialWorldToLocal = node.WorldMatrix.Inverse;
session.BeginInteraction("Resize layer");
// On each pointer sample; never recapture the baseline mid-gesture:
GestureGeometry.ResizeFromHandle(node, baseline, ResizeHandle.Right,
    initialWorldToLocal.Map(pointerInWorldCoordinates), preserveAspect: false, fromCenter: false);
session.Preview();
// On pointer release (or CancelInteraction on Escape):
session.CommitInteraction();
```

Selection resize shares the same handle computation, but per-child min/max constraints can prevent a multi-selection from following a uniform affine scale. Arbitrary skew, flip-through gestures and overriding variable-bound dimensions are not added by this change. Bound dimensions remain source-driven at commit. Typography baseline alignment is a separate, unimplemented layout feature—not the same thing as a gesture's captured baseline.

## Regression coverage

Native tests exercise every handle at four rotations with reflected parent/child transforms, min/max anchoring, proportional side shrinking, centered clamping, wrapping reflow, repeated pointer previews, transaction history and invalid inputs. Browser tests import real fixture documents through the file picker, drag the actual handles, check undo/redo, and inspect downloaded native files to verify that hug-height persists. Tests do not mutate the document through browser diagnostic APIs.
