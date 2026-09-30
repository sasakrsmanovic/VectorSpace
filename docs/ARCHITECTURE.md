# Architecture

## Package boundaries

The ten reusable libraries form an acyclic dependency graph. `Core` owns plain scene/geometry types. `Layout` and `Documents` build on Core. `Editing` composes selection, viewport, transactions, variables and components. `Prototyping` owns isolated playback. `Collaboration` owns the projected protocol, optimistic replica and HTTP connection without depending on Uno. `Skia` supplies rendering and geometry operations. `Controls` contains document-independent Uno controls; `Editor` embeds the canvas and interaction logic; `Workbench` composes authoring, sharing and recovery.

`App` supplies browser/desktop startup and storage. The separately deployed `server/VectorSpace.Server` hosts ASP.NET endpoints and uses the same deterministic layout/variable/component engines as the editor. It does not contain a second rendering or UI implementation.

Engine packages target `net10.0`; Uno libraries target `net10.0-browserwasm` and `net10.0-desktop`. Managed/native Skia versions are pinned to the compatible Uno toolchain.

## Document and transaction contract

Nodes have stable IDs and parent-local double-precision transforms. Parent references are rebuilt after deserialization, not serialized. Cloning can retain or regenerate identities and remaps internal references. Native schema 7 migrates schemas 1–6; shared protocol state is a separate representation rather than a new native file format.

`EditorSession` owns the local selection, active page, viewport and current gesture. Previews mutate inside one transaction. A commit validates variable graphs, resolves bindings, synchronizes component definitions, resolves instance-context bindings, arranges layout, validates the result and captures the final document. Exceptions leave the pre-edit snapshot available for cancellation. Expected commit failures at pointer/text input boundaries roll back instead of propagating through Uno's event loop.

Local history records bounded before/after snapshots. Connected history instead delegates to `ISharedEditorHistory`; it creates guarded property inverses and never restores a stale whole document over collaborators. Local rollback snapshots still exist during a gesture. Shared application preserves selection/page/viewport/expanded rows where identities survive. Network model delivery waits for active pointer, text, asynchronous command and modal boundaries; presence-only delivery can continue.

## Authoritative collaboration

`DocumentProjection` maps native JSON to identity-addressed entity/property cells. Stable-ID lists represent pages, layers, comments, collections, variables and modes. Hierarchy and decimal ranks are separate cells; non-identity arrays and objects remain atomic properties. A per-call read-only JSON DOM and reusable UTF-8 writer retain unchanged dictionary keys and canonical values. A test-only original implementation is the independent correctness oracle.

`SharedReplica` previews bounded pending edits and assigns dependency guards for earlier local transactions. `TransactionEngine.Prepare` checks permissions, prior values and versions, rejects an entire conflicting gesture, validates hierarchy, and invokes the host's deterministic normalization. Independent properties merge; concurrent comment suffixes have an explicit append merge. This is not a general text/vector CRDT.

The service serializes each room under a gate. Checksummed journal frames and receipts are flushed before acknowledgement; contiguous replay restores state and duplicate-request results. Incomplete trailing frames are truncated, while complete checksum corruption fails closed. One process exclusively owns the data directory. There is no distributed consensus or automatic compaction.

`RoomTransport` uses generated JSON metadata, Authorization headers, reviewed HTTPS endpoints and disabled redirects. `CollaborationConnection` owns polling/sending/presence lifecycles and explicit host dispatch. Presence is ephemeral, coalesced and separate from document revision/history. Capability invitations are hashed at rest, role checked on every operation and rechecked after long-poll wakeup. See [protocol semantics](COLLABORATION.md).

## Rendering and interaction

`SceneRenderer` retains bounded geometry, typeface/text, image, gradient and effect resources. Traversal applies nested transforms, clipping, layered paints, blend modes, supported shadow/blur stacks and conservative culling. Shared path construction feeds rendering, picking and SVG export. The Uno canvas derives from `SKCanvasElement`; there is no parallel HTML editor.

Handles use screen coordinates; geometry/snapping use world coordinates converted to parent coordinates for updates. Gesture-scoped snapping uses an immutable sorted coordinate index. PNG export uses a separate surface without chrome. `RemotePresenceLayer` paints retained cursor/selection geometry independently and never clears the lower scene canvas. `ParticipantStrip` has no network dependency. Prototype playback uses a private model and viewport rather than mutating the authoring scene.

## Storage and trust

`IWorkspaceStorage` separates file open/save and ordinary local recovery from UI. Browser interop wraps IndexedDB, file inputs and Blob downloads. `ISharedRecoveryStorage` adds independent per-writer token-free journals for pending/rejected work; reload recovery requires review instead of automatic replay. Desktop adapters use atomic profile-file replacement. Browser shutdown can interrupt a write, so explicit exports remain important.

Generated document/protocol metadata avoids reflection-dependent serialization. SVG XML prohibits DTDs and external resolution. Import validation precedes document replacement. Invitation fragments are removed from browser history and reviewed before any server connection. Shared documents are readable by the service operator; capability links are not verified account identities or end-to-end encryption. See [security](../SECURITY.md) and [hosting](HOSTING.md).

## Validation and limits

Model/editor tests cover exact projection, hierarchy, conditional history and rollback; actual HTTP tests cover authorization, concurrency, idempotence and restart. Multi-window Playwright cases operate published Uno controls with real pointer/keyboard/clipboard events. Opt-in diagnostics are read-only and exclude sensitive invitation values.

CI retains test reports, screenshots, traces, benchmarks, package archives and a self-hostable server. Pages deploys a successful exact-commit browser artifact, not a backend. These checks do not certify arbitrary scale, all native interactions, enterprise security or complete Figma parity. See [validation](VALIDATION.md), [performance](PERFORMANCE.md) and [feature boundaries](FEATURES.md).
