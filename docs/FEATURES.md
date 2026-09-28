# Implemented behavior and parity boundary

VectorSpace is an original Uno/Skia editor, not a complete reproduction of every Figma product.

## Working end-to-end

Scene nodes and nested transforms; frames/groups/pages; drawing, scoped hierarchy selection, deep selection, sibling navigation, marquee and matching-property selection; baseline-based move/resize/rotation with anchored min/max limits and axis-preserving hug/fill; alt-drag duplicate; visibility/locking; undo/redo and clipboard; alignment/distribution; pen/freehand paths and point movement; text; solid/gradient fills; strokes/dashes; opacity/blends; image paints; independent drop/inner shadows and layer blur; safe JSON/SVG imports; SVG/PNG exports; recovery; local comments; isolated local prototype playback.

**Auto-layout:** horizontal and vertical flow, wrapping, grid fixed/auto/fraction tracks, spans, padding, gap, hug/fill, min/max-constrained redistribution, cross-axis/primary alignment, hidden/absolute children, edge and scale constraints. Alignment controls, wrap/grid configuration and item sizing are editable in the inspector. Text-baseline alignment is not implemented; this does not certify every Figma layout precedence rule.

**Appearance:** bounded PNG/JPEG/WebP importing, normalized embedded PNGs, Fill/Fit/Crop/Tile, rotation, image adjustments and on-canvas crop gestures; per-fill blend and reordering; retained gradient shaders, complete appearance overrides, multiple drop/inner shadows, spread and layer blur. See [images and effects](APPEARANCE.md).

**Local design systems:** component sets; creating/combining variants; per-instance variant property selection; structural-name matching of overrides and stable IDs across swaps; nested component dependencies with cycle/expansion limits; typed color/number/string/boolean variables; typed aliases; collection modes; document and layer mode selection; actual property bindings; local instance binding overrides; removal/reset; undo/redo; native JSON and clipboard dependency round-tripping. See [design systems](DESIGN_SYSTEMS.md).

**Prototyping:** named flow starts; dedicated Uno/Skia player; release-click, enter/leave, press/release, key-chord and delay triggers; ordered actions and typed conditional branches; navigation/back; modal overlays with placement/backdrops/dismissal; frame overflow and scroll-to; runtime variable assignment/toggle/add; interactive local variants; consent-gated HTTP(S) link requests; instant/dissolve/move/push and supported name-matched smart interpolation; restart and editor-state isolation. The Prototype inspector authors these features, and an editable playground demonstrates them. See [prototyping](PROTOTYPING.md) for exact semantics and restrictions.

The palette, panels, properties, rows, color picker, numeric scrubbing, icons and workbench composition are authored controls. Low-level text entry, scrolling, focus, menus, checkboxes and dialogs still use Uno/WinUI primitives. No cloud service or plugin execution is hidden behind a familiar-looking icon.

## Partial compatibility

- **Visual/UI parity:** UI3-style workspace, not pixel-certified across every Figma screen, theme, menu and responsive breakpoint.
- **Text:** wrapping, family/size/weight, alignment, line height and spacing; no mixed rich-text runs or complete OpenType/multi-script editing guarantees.
- **Vectors:** cubic paths and point movement; no full vector networks, independent tangent-handle UI, non-destructive Boolean stack or corner smoothing.
- **Transforms:** rigid transforms, flips and scaling; arbitrary affine skew is not lossless.
- **Components/variables:** local variants and value bindings, not the complete exposed-property system, remote libraries, expressions, publishing workflow, every nested swap override or Figma interchange semantics.
- **Effects:** multiple drop/inner shadows, spread and layer blur; no background blur, full masks, arbitrary effect interleaving or complete Figma effect-blend semantics.
- **SVG:** common primitives, paths, groups, text, embedded images and local/inherited gradient paint servers; viewBox/group scaling; supported effect export. Exact path-bounds normalization, full CSS, use references, clips/masks and filter-graph import remain limited. External resources are never loaded; variables and components are not preserved by SVG.
- **Images:** editable embedded raster paints and basic color-matrix adjustments; no RAW/HDR workflow, wide-gamut certification, image metadata preservation, animated images, full Figma adjustment/cropping fidelity or binary asset archive.
- **Prototype:** supported local smart interpolation, overlays and typed conditions, not arbitrary expression evaluation, vector morphing, reversible while-hover/while-press semantics, drag-scrubbed transitions, springs, media timelines, independent nested/sticky/fixed scrollers, gamepads, remote presentations or complete Figma prototype parity.
- **Accessibility/touch:** named focusable controls and canvas gestures, not certified assistive-technology or small-phone productivity parity.

## Not implemented

Native `.fig`/FigJam interchange; multiplayer/presence; account/team administration; cloud files/version history; synchronized comments; plugin execution; complete Dev Mode; advanced image-editing workflows; branching; and the complete Figma product ecosystem.

`Share` explains local-only storage and downloads a real editable file instead of inventing a collaboration link. Prototype playback is private to the current application instance and is not a hosted collaboration service.
