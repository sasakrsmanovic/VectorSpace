# Implemented behavior and parity boundary

VectorSpace is an original Uno/Skia editor, not a complete reproduction of every Figma product.

## Working end-to-end

Scene nodes and nested transforms; frames/groups/pages; drawing, selection, move/resize/rotation; alt-drag duplicate; marquee/deep selection; visibility/locking; undo/redo and clipboard; alignment/distribution; pen/freehand paths and point movement; text; solid/gradient fills; strokes/dashes; opacity/blends; drop shadow; linear auto-layout and edge/scale constraints; local components and overrides; safe JSON/SVG imports; SVG/PNG exports; recovery storage; local comments; immediate click-to-frame prototypes.

The floating palette, panels, properties, rows, color picker, numeric scrubbing, icons and workbench composition are authored controls. Low-level text entry, scrolling, focus, menus, checkboxes and dialogs still use Uno/WinUI primitives rather than reimplementing platform text/focus systems.

## Partial compatibility

- **Visual parity:** UI3-style layout and density, not pixel-certified across every Figma screen, theme, menu and responsive breakpoint.
- **Text:** wrapping, family/size/weight, alignment, line height and spacing; no mixed rich-text runs or complete OpenType/multi-script editing guarantees.
- **Vectors:** cubic paths and point movement; no full vector networks, independent tangent-handle editing UI, non-destructive Boolean stack or corner smoothing.
- **Auto-layout:** horizontal/vertical linear layout, padding, gap, hug/fill and cross-axis alignment; no wrap/grid/baseline system or all Figma sizing precedence rules.
- **Transforms:** rigid transforms, flips and scaling; arbitrary affine skew is not lossless in this model.
- **Components:** local linked instances and text/fill overrides, not full variants/exposed properties/remote libraries/variable collections.
- **Effects:** the first enabled drop shadow is rendered; no inner shadow, background blur, advanced masks or multi-shadow compositor.
- **SVG:** common primitives, paths, groups, text and inline/inherited presentation. Referenced gradients use a reported fallback. External images, use references, stylesheets, filters and masks are not fully imported. Exported text wrapping may differ from canvas wrapping.
- **Prototype:** immediate navigation, not smart animate, overlays, variables, timelines or remote presentations.
- **Accessibility/touch:** named focusable controls and canvas gestures, not certified assistive-technology or small-phone productivity parity.

## Not implemented

Native `.fig`/FigJam interchange; multiplayer/presence; account/team administration; cloud file/version history; synchronized comments; plugins; complete Dev Mode/inspect; variable modes/expressions; full image-editing/fill workflows; branching; and the complete Figma product ecosystem.

Do not infer these from a familiar-looking icon. `Share` explicitly explains local-only storage and downloads an editable file instead of creating a fake collaboration link.
