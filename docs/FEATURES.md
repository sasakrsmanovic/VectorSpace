# Implemented behavior and compatibility boundary

VectorSpace is an original Uno/Skia design editor, not a complete reproduction of every Figma product. Source 0.8 adds independent corners, ellipse arcs/rings, stroke geometry controls, live Boolean operands, exact native contours and direct shape grips. The browser deployment is a static client; sharing requires the separately configured collaboration backend.

## Implemented workflows

Scene nodes/nested transforms; frames/groups/pages; scoped/deep/marquee/sibling/matching-property selection; anchored move/resize/rotation and axis-preserving hug/fill; Alt-drag duplication; visibility/locking; local and guarded shared undo/redo; dependency-aware clipboard/paste in place; alignment/distribution; pen/freehand paths and multi-anchor editing; whole-layer text; layered paints; strokes, images and effects; native JSON/SVG import and SVG/PNG output; recovery, comments and isolated prototypes.

**Shape editing:** independent corner radii, signed ellipse sectors/rings/open arcs, direct corner/arc grips, modifier gestures and cancellation; centered/inside/outside stroke regions, caps/joins/miter/dash phase; live Boolean groups retaining editable operands, operation changes, release and exact native flattening; separate filled stroke contours, including zero-width/height lines; native rational conics and compound paths. Painting/picking share retained stroke geometry. See [shapes](SHAPES.md).

**Editing workflows:** style-only clipboard, selective property paste, variable-value materialization, supported instance style/name records, previewed batch rename and stable layer ordering. Save restores authoring focus. Style clipboard packet 2 retains new stroke descriptors while accepting packet 1. Independent corner arrays are not transferred as style data; copying an independent shape omits the inactive uniform radius. Partial opacity/blend edits preserve inherited corners, while explicit uniform-radius edits replace them. See [editing workflows](EDITING_WORKFLOWS.md).

**Auto-layout:** horizontal/vertical flow, wrap, grid tracks/spans, padding/gaps, hug/fill, min/max redistribution, alignment, hidden/absolute children and edge/scale constraints. Typography baseline alignment and every Figma precedence rule remain unimplemented.

**Appearance:** bounded raster import and normalization, Fill/Fit/Crop/Tile, direct image cropping, adjustments, layered paints/blends, retained gradients and independent shadow/blur stacks. See [appearance](APPEARANCE.md).

**Design systems:** local components/variants, supported overrides, structural matching, stable IDs, bounded nested dependencies and typed variables/aliases/modes. Stroke geometry and explicit uniform-radius overrides survive synchronization. Independent shape/structure editing in instances requires changing the main component or detaching. See [design systems](DESIGN_SYSTEMS.md).

**Prototypes:** named flows, dedicated player, supported triggers/actions/conditions, navigation/Back, overlays, scrolling, private variables/variants, consent-gated links and supported transitions. Presentation clipping uses independent corners; unpainted Boolean operands do not become independent hotspots. See [prototype semantics](PROTOTYPING.md).

**Collaboration:** independent clients, guarded identity-addressed property/hierarchy batches, optimistic edits, conditional own undo, reconnect/retry, recoverable conflicts, comment append merging, presence/following, revocable roles and durable revisions. Schema-4/5 rooms upgrade after replay through a durable system revision. See [collaboration](COLLABORATION.md) and [hosting](HOSTING.md).

The palette, properties, rows/icons, numeric/color controls, shape editors/grips, transfer/rename panels, participant avatars and presence overlay are authored components. Low-level text entry, focus, scrolling, menus, checkboxes and dialogs still use Uno primitives.

## Partial compatibility

- **UI:** UI3-style workspace, not pixel-certified against every screen, theme, breakpoint or accessibility workflow.
- **Text:** wrapping and whole-layer typography; no mixed rich-text runs or complete OpenType/multi-script editing guarantees.
- **Vectors:** editable single contours and handles, native compound-path rendering/persistence, live Booleans and stroke outlines; no full vector networks, compound-anchor topology editor, shape builder, pressure/variable-width strokes or corner smoothing.
- **Boolean geometry:** retained operand coordinate frames and existing group/constraint resizing; not complete Figma automatic bounds/reflow, arbitrary nonuniform affine scaling or structural instance editing. Operand fills are the silhouettes; outline an open stroke to use its area.
- **Transforms/layout:** supported rigid/scaled transforms, constraints and flow/grid behavior; arbitrary skew and text-baseline alignment remain incomplete.
- **Components/properties:** local supported overrides/bindings, not full nested exposure, published libraries or every Figma property. Geometry-level independent radii/arc editing in instances remains restricted.
- **Naming:** bounded linear-time regex matching; lookarounds/pattern backreferences are rejected. Browser-reserved shortcuts have menu alternatives.
- **Effects/SVG/images:** supported stacks, not full masks/background blur, effect ordering, CSS/use/filter import, lossless metadata, animated/RAW/HDR or wide-gamut certification. SVG conics are adaptively approximated; native conics remain exact. Live Boolean/aligned-stroke export requires the renderer-aware API.
- **Prototypes:** supported interpolation, not arbitrary expressions, path morphing, reversible hover/press, drag-scrubbing, springs/media, independent nested/sticky scrolling, gamepads or synchronized remote presentations.
- **Sharing:** guarded property batches, not a text/vector CRDT or Figma wire protocol. Same-property conflicts retain recovery copies; recovery after reload requires review. Capability links are not SSO identity; history is a bounded single-process journal.

## Not implemented

Native `.fig`/FigJam interchange; complete team/account administration and SSO; managed cloud storage/backups and account-wide search; remote library publishing; plugins; complete Dev Mode; branching/merging; advanced rich-text/vector-network/image workflows; and the complete Figma ecosystem.

A static Pages deployment, server template or passing ephemeral-backend CI run is not proof of a public backend deployment. Current native schema **6** rejects silent downgrade. Upgrade clients and self-hosted servers together and retain backups.
