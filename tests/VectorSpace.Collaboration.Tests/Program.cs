using System.Text.Json;
using VectorSpace.Collaboration;
using VectorSpace.Core;
using VectorSpace.Documents;

var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
void Throws<T>(Action run) where T : Exception { try { run(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
DesignDocument Document() => new() { Id = "doc", Pages = [new() { Id = "page", Nodes = [new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B", X = 200 }] }], Comments = [new() { Id = "thread", PageId = "page", Text = "Review", CreatedAt = DateTimeOffset.UnixEpoch }] };
SharedSnapshot Initial() => DocumentProjection.FromDocument(Document());
EditBatch Batch(SharedSnapshot state, Action<DesignDocument> edit, string actor = "actor", long sequence = 1)
{
    var doc = DocumentProjection.ToDocument(state); edit(doc);
    return new(Guid.NewGuid().ToString("N"), actor, sequence, "Edit", state.Revision, DocumentProjection.Diff(state, DocumentProjection.FromDocument(doc, state)));
}
Commit CommitBatch(TransactionEngine engine, EditBatch batch) { var c = engine.Prepare(batch, RoomRole.Editor, "Test"); engine.Accept(c); return c; }
void AcceptReplica(TransactionEngine engine, SharedReplica replica)
{
    var c = CommitBatch(engine, replica.NextBatch()!);
    replica.Receive(new() { Revision = c.Revision, Commits = [c], Acknowledged = c });
}
void EditReplica(SharedReplica replica, double x)
{
    var doc = DocumentProjection.ToDocument(replica.Visible); doc.Find("a")!.X = x;
    replica.Submit(DocumentJson.Save(doc), "Move");
}

Test("projection roundtrip preserves native schema and all stable identities", () => {
    var doc = Document(); var state = DocumentProjection.FromDocument(doc);
    Check(DocumentJson.Save(doc) == DocumentJson.Save(DocumentProjection.ToDocument(state)));
    Check(DocumentProjection.Diff(state, DocumentProjection.FromDocument(DocumentProjection.ToDocument(state), state)).Count == 0);
});
Test("independent properties on the same node merge", () => {
    var s = Initial(); var e = new TransactionEngine(s);
    CommitBatch(e, Batch(s, d => d.Find("a")!.X = 30));
    CommitBatch(e, Batch(s, d => d.Find("a")!.Y = 50, "other"));
    var a = DocumentProjection.ToDocument(e.State).Find("a")!; Check(a.X == 30 && a.Y == 50);
});
Test("same property rejects a whole transaction atomically", () => {
    var s = Initial(); var e = new TransactionEngine(s); CommitBatch(e, Batch(s, d => d.Find("a")!.X = 30));
    Throws<CollaborationConflictException>(() => e.Prepare(Batch(s, d => { d.Find("a")!.X = 40; d.Find("b")!.Y = 90; }), RoomRole.Editor, "B"));
    Check(e.State.Revision == 1 && DocumentProjection.ToDocument(e.State).Find("b")!.Y == 0);
});
Test("concurrent independent insertions retain both layers", () => {
    var s = Initial(); var e = new TransactionEngine(s);
    CommitBatch(e, Batch(s, d => d.Pages[0].Nodes.Insert(1, new() { Id = "c" })));
    CommitBatch(e, Batch(s, d => d.Pages[0].Nodes.Insert(1, new() { Id = "d" }), "other"));
    var d = DocumentProjection.ToDocument(e.State); Check(string.Join(',', d.Pages[0].Nodes.Select(n => n.Id)) == "a,c,d,b");
    Check(DocumentProjection.Diff(e.State, DocumentProjection.FromDocument(d, e.State)).Count == 0, "Tied ranks must not generate phantom edits.");
});
Test("concurrent replies append without losing either message", () => {
    var s = Initial(); var e = new TransactionEngine(s);
    CommitBatch(e, Batch(s, d => d.Comments[0].Replies.Add("First")));
    CommitBatch(e, Batch(s, d => d.Comments[0].Replies.Add("Second"), "other"));
    Check(DocumentProjection.ToDocument(e.State).Comments[0].Replies.SequenceEqual(new[] { "First", "Second" }));
});
Test("viewer permissions are server enforced", () => {
    var s = Initial(); Throws<UnauthorizedAccessException>(() => new TransactionEngine(s).Prepare(Batch(s, d => d.Find("a")!.X++), RoomRole.Viewer, "Viewer"));
});
Test("commenter may reply but cannot alter the design", () => {
    var s = Initial(); var e = new TransactionEngine(s);
    e.Accept(e.Prepare(Batch(s, d => d.Comments[0].Replies.Add("Approved")), RoomRole.Commenter, "Review"));
    Throws<UnauthorizedAccessException>(() => e.Prepare(Batch(e.State, d => d.Find("a")!.X++), RoomRole.Commenter, "Review"));
});
Test("parent cycles cannot enter authoritative state", () => {
    var d = Document(); d.Pages[0].Nodes[0].Kind = NodeKind.Frame; d.Pages[0].Nodes[0].Add(new() { Id = "child", Kind = NodeKind.Frame });
    var s = DocumentProjection.FromDocument(d); var key = DocumentProjection.Key("node:a", "@parent");
    var slot = DocumentProjection.Key("node:a", "@slot");
    Throws<InvalidDataException>(() => new TransactionEngine(s).Prepare(new("cycle", "a", 1, "Cycle", 0,
        [new(key, s.Value(key), "\"node:child\""), new(slot, s.Value(slot), "\"children\"")]), RoomRole.Editor, "A"));
});
Test("delete cannot discard a concurrent child edit", () => {
    var d = Document(); d.Pages[0].Nodes[0].Add(new() { Id = "child" }); var s = DocumentProjection.FromDocument(d); var e = new TransactionEngine(s);
    var deletion = Batch(s, doc => doc.Pages[0].Nodes.RemoveAt(0));
    CommitBatch(e, Batch(s, doc => doc.Find("child")!.X++));
    Throws<CollaborationConflictException>(() => e.Prepare(deletion, RoomRole.Editor, "A"));
    Check(DocumentProjection.ToDocument(e.State).Find("child")!.X == 1);
});
Test("expanded layer rows remain local", () => {
    var s = Initial(); var b = Batch(s, d => d.Find("a")!.Expanded = false); Check(b.Changes.Count == 0);
});
Test("projection rejects orphaned records", () => {
    var s = Initial(); s.Cells[DocumentProjection.Key("node:a", "@parent")] = "\"node:missing\"";
    Throws<InvalidDataException>(() => DocumentProjection.ToDocument(s));
});
Test("duplicate cell updates cannot defeat atomic guards", () => {
    var s = Initial(); var b = Batch(s, d => d.Find("a")!.X = 5); b.Changes.Add(b.Changes[0]);
    Throws<InvalidDataException>(() => new TransactionEngine(s).Prepare(b, RoomRole.Editor, "A"));
});
Test("optimistic queue resolves dependencies without waiting to edit", () => {
    var s = Initial(); var r = new SharedReplica(s, "a"); var e = new TransactionEngine(s);
    EditReplica(r, 1); EditReplica(r, 2); Check(r.PendingCount == 2 && DocumentProjection.ToDocument(r.Visible).Find("a")!.X == 2);
    AcceptReplica(e, r); Check(r.PendingCount == 1); AcceptReplica(e, r);
    Check(r.PendingCount == 0 && DocumentProjection.ToDocument(e.State).Find("a")!.X == 2);
});
Test("progressive own undo and redo retain version guards", () => {
    var s = Initial(); var r = new SharedReplica(s, "a"); var e = new TransactionEngine(s);
    EditReplica(r, 1); AcceptReplica(e, r); EditReplica(r, 2); AcceptReplica(e, r);
    Check(r.Undo()); AcceptReplica(e, r); Check(r.Undo()); AcceptReplica(e, r); Check(DocumentProjection.ToDocument(e.State).Find("a")!.X == 0);
    Check(r.Redo()); AcceptReplica(e, r); Check(r.Redo()); AcceptReplica(e, r); Check(DocumentProjection.ToDocument(e.State).Find("a")!.X == 2);
    Check(r.Undo()); AcceptReplica(e, r); Check(r.Undo()); AcceptReplica(e, r); Check(DocumentProjection.ToDocument(e.State).Find("a")!.X == 0);
});
Test("undo does not overwrite later peer values", () => {
    var s = Initial(); var r = new SharedReplica(s, "a"); var e = new TransactionEngine(s);
    EditReplica(r, 1); AcceptReplica(e, r);
    var peer = CommitBatch(e, Batch(e.State, d => d.Find("a")!.X = 9, "peer")); r.Receive(new() { Commits = [peer] });
    Check(!r.Undo() && r.PendingCount == 0 && DocumentProjection.ToDocument(r.Visible).Find("a")!.X == 9);
});
Test("undo of one property preserves a peer's other property", () => {
    var s = Initial(); var r = new SharedReplica(s, "a"); var e = new TransactionEngine(s);
    EditReplica(r, 1); AcceptReplica(e, r);
    var peer = CommitBatch(e, Batch(e.State, d => d.Find("a")!.Y = 9, "peer")); r.Receive(new() { Commits = [peer] });
    Check(r.Undo()); AcceptReplica(e, r); var a = DocumentProjection.ToDocument(r.Visible).Find("a")!; Check(a.X == 0 && a.Y == 9);
});
Test("rejected changes retain an exportable recovery document", () => {
    var r = new SharedReplica(Initial(), "a"); EditReplica(r, 42); var batch = r.NextBatch()!;
    r.Receive(new() { Receipt = new(batch.Id, batch.Sequence, false, 0, "Conflict") });
    Check(r.PendingCount == 0 && r.Recovery.Count == 1 && DocumentJson.Load(r.Recovery[0].Document).Find("a")!.X == 42);
});
Test("duplicate acknowledgements do not duplicate undo history", () => {
    var r = new SharedReplica(Initial(), "a"); var e = new TransactionEngine(Initial()); EditReplica(r, 1);
    var c = CommitBatch(e, r.NextBatch()!); r.Receive(new() { Commits = [c], Acknowledged = c }); r.Receive(new() { Commits = [c] });
    Check(r.History.Count == 1);
});
Test("an undo stale after an equal-value peer rewrite still cannot override the peer", () => {
    var r = new SharedReplica(Initial(), "a"); var e = new TransactionEngine(Initial()); EditReplica(r, 1); AcceptReplica(e, r);
    var c = CommitBatch(e, Batch(e.State, d => d.Find("a")!.X = 2, "peer")); r.Receive(new() { Commits = [c] });
    c = CommitBatch(e, Batch(e.State, d => d.Find("a")!.X = 1, "peer", 2)); r.Receive(new() { Commits = [c] }); Check(!r.Undo());
});
Test("wire serialization uses generated metadata", () => {
    var reply = new SyncReply { Role = RoomRole.Editor, Snapshot = Initial() };
    var text = JsonSerializer.Serialize(reply, CollaborationJson.Default.SyncReply);
    var copy = JsonSerializer.Deserialize(text, CollaborationJson.Default.SyncReply)!;
    Check(copy.Role == RoomRole.Editor && copy.Snapshot!.Cells.Count == reply.Snapshot.Cells.Count);
});

var failures = 0;
foreach (var (name, run) in tests) { try { run(); Console.WriteLine("PASS " + name); } catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + "\n" + error); } }
Console.WriteLine($"COLLABORATION RESULT: {tests.Count - failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;
