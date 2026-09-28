# Implemented behavior and compatibility boundary

VectorSpace is an original Uno/Skia design editor, not a complete reproduction of every Figma product. Source version 0.6 adds actual self-hosted collaboration; the public Pages deployment remains a static client and requires a separately configured backend for shared files.

## Working end to end

Scene nodes and nested transforms; frames/groups/pages; drawing, scoped hierarchy selection, deep selection, sibling navigation, marquee and matching-property selection; baseline move/resize/rotation with anchored min/max limits and axis-preserving hug/fill; Alt-drag duplication; visibility/locking; local or conditional shared undo/redo; clipboard and paste in place; alignment/distribution; pen/freehand paths, multi-anchor editing and exact cubic splitting; text; layered paints; strokes/dashes; opacity/blends; image fills; independent drop/inner shadows and layer blur; safe native JSON/SVG imports; SVG/PNG exports; local recovery; comments; isolated prototype playback.

**Auto-layout:** horizontal/vertical flow, wrapping, grid fixed/auto/fraction tracks, spans, padding/gaps, hug/fill, min/max redistribution, alignment, hidden/absolute children, edge and scale constraints. Text-baseline alignment is not implemented; this is not certification of every Figma layout precedence rule.

**Appearance:** bounded PNG/JPEG/WebP import and orientation-normalized embedded PNGs; Fill/Fit/Crop/Tile, rotation, image adjustments and direct crop gestures; per-fill blend/reordering; retained gradients, full appearance overrides, multiple shadows and blur. See [appearance](APPEARANCE.md).

**Design systems:** local component sets/variants, named instance variant selection, structural override matching, stable IDs across swaps, bounded nested dependencies, typed variables/aliases/modes and supported property bindings. Native clipboard/file operations preserve dependencies. See [design systems](DESIGN_SYSTEMS.md).

**Prototypes:** named flows; dedicated player; click/enter/leave/press/release/key/delay triggers; ordered actions and conditions; navigation/Back; modal overlay stacks; scrolling; private runtime variables and variants; consent-gated links; instant/dissolve/move/push and supported name-matched smart interpolation. See [prototype semantics](PROTOTYPING.md).

**Collaboration:** real independent browser/desktop clients, identity-addressed property and hierarchy transactions, optimistic queues, authoritative ordering, conditional per-user undo, same-window reconnect/retry, recoverable conflicts, concurrent comment reply append, presence/cursors/selections, participant following, viewer/commenter/editor/owner capabilities, revocable guest invitations, durable room journals and revision download/conditional restore. The server normalizes merged layout, variables and components before persisting. See [collaboration](COLLABORATION.md) and [hosting](HOSTING.md).

The palette, properties, rows, icons, numeric scrubbing, color picker, participant avatars, remote-presence overlay and workbench composition are authored components. Low-level text entry, scrolling, focus, menus, checkboxes and dialogs still use Uno/WinUI primitives.

## Partial compatibility

- **Visual/UI:** UI3-style workspace, not pixel-certified across all Figma screens, themes, menus, responsive breakpoints or assistive technologies.
- **Text:** wrapping, family/size/weight, alignment, line height and spacing, but no mixed rich-text runs or complete OpenType/multi-script editing guarantees.
- **Vectors:** editable single contours, multi-point selection, handles, exact cubic insertion, conversion, reversal/closure and simplification. Vector networks, multi-contour topology editing, non-destructive Boolean stacks, variable-width strokes and corner smoothing remain absent. See [editing](EDITING.md).
- **Transforms:** rigid/scaled transforms and flips; arbitrary skew is not lossless.
- **Components/variables:** supported local definitions and bindings, not full exposed/nested properties, remote libraries, publishing, expressions or every swap override semantic.
- **Effects/SVG/images:** supported image and effect stacks, not full masks/background blur, arbitrary effect interleaving, complete SVG CSS/use/filter import, lossless image metadata, RAW/HDR, animated assets or wide-gamut certification.
- **Prototypes:** supported local interpolation and transitions, not arbitrary expressions, vector morphing, reversible while-hover/press, drag-scrubbing, springs, media, independent nested/sticky/fixed scrolling, gamepads or synchronized remote presentations.
- **Collaboration:** property-level guarded transactions, not a text/vector CRDT or Figma wire protocol. Same-property/atomic-array conflicts retain recovery copies instead of merging individual characters or path anchors. Local recovery after reload requires review, not automatic offline replay. Capability links are not account/SSO identity; display names are self-reported. Server history is a bounded single-process journal, not a distributed cloud file/version platform.

## Not implemented

Native `.fig`/FigJam interchange; full account/team administration and SSO; managed cloud storage/backups and account-wide file search; remote published libraries; plugin execution; complete Dev Mode; branching/branch merging; advanced rich-text/vector-network/image-editing workflows; and the complete Figma product ecosystem.

The Share dialog now creates or joins real rooms and manages invitations against a user-selected service. It does not invent a public room URL or silently upload a local document. GitHub Pages hosts only the client. A server template or successful ephemeral-backend CI test is not proof that a public backend was deployed.
