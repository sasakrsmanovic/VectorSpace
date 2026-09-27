# Implemented behavior and parity boundary

VectorSpace is an original Uno/Skia editor, not a complete reproduction of every Figma product.

## Working end-to-end

Scene nodes and nested transforms; frames/groups/pages; drawing, scoped hierarchy selection, deep selection, sibling navigation, marquee and matching-property selection; baseline-based move/resize/rotation; alt-drag duplicate; visibility/locking; undo/redo and clipboard; alignment/distribution; pen/freehand paths and point movement; text; solid/gradient fills; strokes/dashes; opacity/blends; first drop shadow; safe JSON/SVG imports; SVG/PNG exports; recovery; local comments; immediate click-to-frame prototypes.

**Auto-layout:** horizontal and vertical flow, wrapping, grid fixed/auto/fraction tracks, spans, padding, gap, hug/fill, min/max-constrained redistribution, cross-axis/primary alignment, baseline estimates, hidden/absolute children, edge and scale constraints. Alignment controls, wrap/grid configuration and item sizing are editable in the inspector. This does not certify every Figma precedence or text-baseline case.

**Local design systems:** component sets; creating/combining variants; per-instance variant property selection; structural-name matching of overrides and stable IDs across swaps; nested component dependencies with cycle/expansion limits; typed color/number/string/boolean variables; typed aliases; collection modes; document and layer mode selection; actual property bindings; local instance binding overrides; removal/reset; undo/redo; native JSON and clipboard dependency round-tripping. See [design systems](DESIGN_SYSTEMS.md).

The palette, panels, properties, rows, color picker, numeric scrubbing, icons and workbench composition are authored controls. Low-level text entry, scrolling, focus, menus, checkboxes and dialogs still use Uno/WinUI primitives. No cloud service or plugin execution is hidden behind a familiar-looking icon.

## Partial compatibility

- **Visual/UI parity:** UI3-style workspace, not pixel-certified across every Figma screen, theme, menu and responsive breakpoint.
- **Text:** wrapping, family/size/weight, alignment, line height and spacing; no mixed rich-text runs or complete OpenType/multi-script editing guarantees.
- **Vectors:** cubic paths and point movement; no full vector networks, independent tangent-handle UI, non-destructive Boolean stack or corner smoothing.
- **Transforms:** rigid transforms, flips and scaling; arbitrary affine skew is not lossless.
- **Components/variables:** local variants and value bindings, not the complete exposed-property system, remote libraries, expressions, publishing workflow, every nested swap override or Figma interchange semantics.
- **Effects:** first enabled drop shadow; no inner shadow, background blur, full masks or multi-shadow compositor.
- **SVG:** common primitives, paths, groups, text and inline/inherited presentation. Referenced gradients use a reported fallback. External images, use references, stylesheets, filters and masks are not fully imported. Variables and component relationships are not preserved by SVG.
- **Prototype:** immediate navigation; no smart animate, overlays, conditional expressions, timelines or remote presentations.
- **Accessibility/touch:** named focusable controls and canvas gestures, not certified assistive-technology or small-phone productivity parity.

## Not implemented

Native `.fig`/FigJam interchange; multiplayer/presence; account/team administration; cloud files/version history; synchronized comments; plugin execution; complete Dev Mode; full image-editing/fill workflows; branching; and the complete Figma product ecosystem.

`Share` explains local-only storage and downloads a real editable file instead of inventing a collaboration link.
