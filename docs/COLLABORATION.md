# Collaborative editing

VectorSpace 0.6 adds a self-hostable collaboration service and an integrated Uno client. This is real networked document editing, not a same-tab simulation: independent browser/desktop instances exchange ordered document transactions, live presence and comments with an ASP.NET service.

**The public GitHub Pages site is the client only.** It does not contain or provision the backend. Use your own HTTPS deployment, or a loopback development service. There is no default public room service, account subscription, hidden third-party document upload or automatic connection to an invitation's server.

## Start working together

Run the service using the [hosting guide](HOSTING.md). Open **Share**, enter your display name and choose either workflow:

- **Create shared file:** enter the service URL and its room-creation key, then confirm uploading the complete current design. Save the owner access link privately. The service persists the file and its subsequent committed revisions.
- **Join file:** paste a VectorSpace invitation and confirm its server address. Joining replaces this editor's document; it does not upload unrelated local content. Save a local copy first.

After creating a room, use **Share → Create invitation** to produce a separately revocable viewer, commenter or editor link. A participant can follow another person's page and viewport by clicking their avatar. A local canvas press or wheel gesture stops following. Remote cursors and selection outlines are drawn independently of the design canvas and do not become document layers.

An invitation opened in the browser is read from its URL fragment and removed from browser history before presentation. The Share dialog then asks for explicit confirmation; merely opening the link does not contact its server. Tokens are not placed in URL query parameters, diagnostics or local recovery journals. They are sent to the reviewed endpoint in an Authorization header. HTTP redirects are disabled.

## Permissions and identity

| Invitation | Allowed actions |
|---|---|
| Viewer | Read, inspect, select, follow participants, download versions and present locally. No shared mutations. |
| Commenter | Viewer actions plus adding, replying to and resolving shared comments. No design-property mutations. |
| Editor | Edit shared document properties, hierarchy, variables, components and comments; conditionally restore a revision. No invitation administration. |
| Owner | Editor actions plus creating, listing and revoking guest invitations. |

Permissions are checked on the server, not merely represented by disabled controls. The owner cannot mint another owner through the guest invitation endpoint. Revocation removes the invitation's active presence and rejects subsequent polling, reading and editing requests, including a long poll that was already waiting.

This is **capability-link access**, not account authentication. Anyone holding a link has its role. Display names are self-reported. Server history attributes commits to the owner-defined invitation label; separate invitations make that attribution more useful. Comments remain ordinary editable design data, not a tamper-proof audit record. Revocation cannot erase files already downloaded by a guest. Team accounts, SSO, enterprise directory administration, an invitation-email service and account recovery are not implemented.

## Transaction model

The reusable `VectorSpace.Collaboration` package projects the native document into identity-addressed cells. Each page, layer, comment, variable, collection and mode has its own entity. Hierarchy membership and order are separate cells; values without stable identities, such as a fill stack, path-point array or layout descriptor, are atomic properties.

A gesture commits one batch of changed cells, carrying each cell's previous value and acknowledged version. The server processes batches under a room-specific lock, validates their guards and native document structure, recomputes layout/variable/component-derived data, persists the resulting event, and only then acknowledges it. Independent properties can merge, including X and Y on the same layer. A conflicting cell rejects the **whole gesture**, rather than applying part of a drag or silently overwriting the peer.

This is an authoritative, optimistically edited protocol, **not a general CRDT and not binary-compatible with Figma's multiplayer protocol**. Simultaneous edits to the same scalar, rich-text string, path-point array or paint stack do not character/anchor-merge. The rejected edit remains available as a native recovery file. Concurrent comment replies have a specific append merge that preserves both suffixes when both were based on the same conversation prefix.

Sibling ranks are preserved during ordinary property edits. Independent insertions into the same gap use identity as a deterministic tie-breaker. Explicit reorder or exhausted fractional spacing can renumber a sibling list; normal guards then protect against concurrent structural edits. Cycles, orphaned records and invalid native references are rejected.

## Optimistic edits and undo

The local canvas remains responsive while up to eight edits await acknowledgement. There is one in-flight document request per client. Subsequent edits can depend on earlier local versions; acknowledgement resolves those dependencies to server revisions. Retrying an acknowledged transaction after a connection loss or server restart returns its durable receipt rather than applying it twice.

While connected, `EditorSession` delegates history to `ISharedEditorHistory`. It does **not** restore whole-document snapshots for undo. Each own history entry has per-property guards. Undo creates an inverse transaction only while those guarded values still belong to that entry. A later peer write, including a write away and back to the same value, prevents the old undo from replacing it. Progressive own undo/redo retargets only precisely exposed historical versions. Hierarchy-invalid inverse operations are skipped rather than dropping another participant's descendants.

Remote model application is deferred while a pointer gesture, inline text edit, asynchronous workbench command or modal inspector transaction is active. Presence may continue during that interval. Network receivers await the boundary, so this does not accumulate an unbounded sequence of remote scene copies. A remote deletion conflicting with pending work never exposes an orphaned partial layer to the renderer. Selection, current page, viewport and local expansion state are preserved where their identities remain valid.

## Disconnection and recovery

A temporary network failure retries with bounded exponential backoff. Edits remain in the current window and synchronize after reconnection. Unacknowledged edits and rejected gestures are also saved to a **separate local recovery journal per writer**, in IndexedDB or the desktop profile. Two windows do not overwrite each other's recovery record. Ordinary synchronized edits do not leave a permanent duplicate recovery file.

After reload, recovery is deliberately **not replayed automatically**. A former edit may have conflicted with newer server work or been accepted just before the connection disappeared. Use **Share → Local collaboration recovery**, download the native copy, rejoin the current shared file and compare before reapplying it. A conflict blocks additional authoring until its recovery has been reviewed or explicitly discarded. Pending edits retain their latest safety copy even when old conflict records are discarded.

Browser shutdown can interrupt an asynchronous local-storage write. Check the synchronization/recovery status, save important work explicitly and keep backups. A pending request can still commit on the server after a window begins leaving; the departure confirmation states this. Offline recovery is not a guarantee of continuous offline coauthoring across arbitrary reloads.

## History and persistence

**Share → Version history** lists the latest 100 committed server revisions, with invitation label, edit label and timestamp. Download reconstructs that revision. Restore creates a **new conditional shared edit** and preserves earlier history; it is not a force overwrite or branch reset.

The service stores initial room metadata and a length-delimited, SHA-256-checksummed append journal. Successful commits and rejected-request receipts are flushed before acknowledgement. Replay repairs an incomplete trailing frame after interruption; a complete checksum mismatch fails closed. The regression suite restarts the actual process and verifies persisted receipts and historical documents. This is not a certification of every filesystem, storage-controller or machine-power-failure scenario.

One process owns a data directory through an exclusive file lock. This implementation does not provide database replication, multi-server consensus, automatic journal compaction, cloud backups or account-wide file search. Back up the data directory using the [hosting procedure](HOSTING.md).

## Performance and resource limits

Ordinary network edits contain deltas, not complete scene snapshots. A retained revision window supplies reconnect deltas; older cursors receive a fresh snapshot. Presence is separate from document transactions and history. Client cursor samples are coalesced at 120 ms; room-wide presence wakeups are coalesced at 100 ms. Idle clients retain a heartbeat, not a permanent animation loop. Presence-only responses skip document projection and materialization.

The presence overlay retains paint, font and cursor geometry, and recomputes selected-layer outlines only when the document or selection signature changes. Document updates continue to reuse the renderer's existing geometry/image/gradient/effect caches. Own acknowledgements with unchanged visible properties do not replace the scene.

Important bounds: 32 resident rooms per process; 32 active participants per room; 128 total invitations including the owner; 2048 remembered client identities per room; 256 MiB journal; 32 MiB native document text; 500,000 projected cells; 50,000 changed cells per gesture; eight pending edits with 64 MiB total pending native text; 150 local shared-history entries with a 32 MiB character budget, except one retained oversized entry. A bounded recovery copy can temporarily coexist with its pending transaction. Encoded JSON overhead, network buffers, native renderer memory and journal replay have additional costs.

These are explicit protective limits, not load-tested capacity guarantees. Projection, validation, canonicalization and materialization still walk document data. Embedded images remain native JSON strings; editing a paint stack can send that complete property. There is no binary asset archive or constant-time commit claim. The collaboration benchmark reports both delta wire size and the measured full projection/diff cost. See [performance](PERFORMANCE.md).

## Reuse and verification

`SharedReplica` and `TransactionEngine` can be tested without Uno or a network. `RoomTransport` and `CollaborationConnection` supply the HTTP lifecycle and explicit host dispatch. `ISharedEditorHistory` integrates conditional history without giving the engine a UI dependency. `ParticipantStrip` and `RemotePresenceLayer` do not depend on a networking library. `ISharedRecoveryStorage` is optional for custom storage hosts; unsupported persistence is shown explicitly instead of silently claimed.

Build runs model/editor regressions, real HTTP/restart tests and tagged multi-window browser cases against the actual compiled service. The ordinary public Pages checks exercise the static editor separately. Passing an ephemeral-backend CI test does not mean a public collaboration server was provisioned. The source, self-hostable server artifact and container/hosting templates are delivered independently from any hosting account.

This increment does not complete native `.fig`, remote published libraries, plugin execution, vector networks, mixed rich text, or every Figma product/UI behavior. See [feature boundaries](FEATURES.md).
