# Implemented behavior and compatibility boundary

VectorSpace is an original Uno/Skia design editor, not a complete reproduction of every Figma product. Source 0.7 adds property transfer, persistent instance style/name overrides, batch naming and stable layer order. Its browser deployment is a static client; sharing requires the separately configured collaboration backend.

## Working end to end

Scene nodes/nested transforms; frames/groups/pages; drawing, scoped/deep/marquee/sibling/matching-property selection; baseline move/resize/rotation with anchored min/max limits and axis-preserving hug/fill; Alt-drag duplication; visibility/locking; local and guarded shared undo/redo; dependency-aware clipboard/paste in place; alignment/distribution; pen/freehand paths, multi-anchor editing and exact cubic splitting; whole-layer text; layered paints; strokes/dashes; opacity/blends; image fills; independent drop/inner shadows and layer blur; native JSON/SVG imports; SVG/PNG output; local recovery; comments and isolated prototypes.

**Editing workflows:** style-only clipboard with complete supported fill/stroke/effect descriptors and whole-layer typography; selective property paste; explicit variable-value materialization; supported instance style/name records; previewed batch rename with current-name/number/capture substitution; stable per-parent layer ordering. Save restores authoring focus after its host operation. See [editing workflows](EDITING_WORKFLOWS.md).

**Auto-layout:** horizontal/vertical flow, wrap, grid fixed/auto/fraction tracks/spans, padding/gaps, hug/fill, min/max redistribution, alignment, hidden/absolute children and edge/scale constraints. Typography baseline alignment and every Figma precedence rule remain unimplemented.

**Appearance:** bounded PNG/JPEG/WebP import, normalized embedded PNGs, Fill/Fit/Crop/Tile, rotation/adjustments, direct crop, fill blend/reordering, retained gradients, appearance overrides, independent shadows and blur. See [appearance](APPEARANCE.md).

**Design systems:** local component sets/variants, instance variant selection, structural override matching, stable IDs, bounded nested dependencies, typed variables/aliases/modes and supported bindings. New stroke/typography/scalar/name overrides survive definition changes and matched swaps. See [design systems](DESIGN_SYSTEMS.md).

**Prototypes:** named flows, dedicated player, click/enter/leave/press/release/key/delay reactions, ordered actions/conditions, navigation/Back, overlays, scrolling, private variables/variants, consent-gated links and supported instant/dissolve/move/push/name-matched smart interpolation. See [prototype semantics](PROTOTYPING.md).

**Collaboration:** independent browser/desktop clients, identity-addressed properties/hierarchy, optimistic edits, authoritative ordering, conditional own undo, reconnect/retry, recoverable conflicts, comment append merging, cursors/selections/following, revocable viewer/commenter/editor/owner capabilities and durable revisions. Schema-4 rooms upgrade after replay using a system revision. See [collaboration](COLLABORATION.md), [hosting](HOSTING.md), and [schema-5 migration](EDITING_WORKFLOWS.md#native-format-and-shared-room-upgrade).

The palette, properties, rows, icons, numeric/color controls, transfer/rename panels, participant avatars, presence overlay and workbench composition are authored components. Low-level text entry, focus, scrolling, menus, checkboxes and dialogs still use Uno primitives.

## Partial compatibility

- **UI:** UI3-style workspace, not pixel-certified against every screen, theme, breakpoint or accessibility workflow.
- **Text:** wrapping and whole-layer typography; no mixed rich-text runs or complete OpenType/multi-script editing guarantees. Property paste preserves target text rather than transferring content or mixed-run ranges.
- **Vectors:** editable single contours, points/handles, exact cubic insertion, conversion, reversal/closure and simplification; no vector networks, compound-contour topology editing, shape builder, nondestructive Boolean stack, pressure/variable-width strokes or corner smoothing.
- **Transforms/layout:** supported rigid/scaled transforms, constraints and flow/grid behavior; arbitrary skew and text-baseline alignment are not complete.
- **Components/properties:** local supported overrides/bindings, not full nested exposure, published libraries or arbitrary structural instance editing. Change the definition or detach before reordering instance children. Property-copy behavior covers explicitly listed groups, not every Figma property.
- **Naming:** deterministic bounded batch naming and linear-time regex matching; lookarounds/pattern backreferences are rejected rather than falling back to an unbounded matcher. Browser-reserved Ctrl+R has F2/menu alternatives.
- **Effects/SVG/images:** supported stacks, not full masks/background blur, arbitrary effect interleaving, complete SVG CSS/use/filter import, lossless metadata, animated/RAW/HDR or wide-gamut certification.
- **Prototypes:** supported local interpolation, not arbitrary expressions, path morphing, reversible hover/press, drag-scrubbing, springs/media, independent nested/sticky scrolling, gamepads or synchronized remote presentations.
- **Sharing:** guarded property batches, not a text/vector CRDT or Figma wire protocol. Same-property/array conflicts retain recovery copies. After reload recovery requires review. Capability links are not account/SSO identity; history is a bounded single-process journal, not a distributed cloud file/version platform.

## Not implemented

Native `.fig`/FigJam interchange; complete team/account administration and SSO; managed cloud storage/backups and account-wide search; remote library publishing; plugins; complete Dev Mode; branching/merging; advanced rich-text/vector-network/image-editing workflows; and the complete Figma ecosystem.

Sharing uses a real user-selected service with explicit consent. A static Pages deployment, server template or passing ephemeral-backend CI run is not proof of a public backend deployment. Current native schema 5 rejects silent downgrade; clients and servers should be upgraded together.
