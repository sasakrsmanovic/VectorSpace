using VectorSpace.Collaboration;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

if (args.Contains("--benchmark")) return CollaborationBenchmarks.Run();
var tests = new List<(string Name, Action Run)>();
void Test(string name, Action run) => tests.Add((name, run));
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
DesignDocument Document() => new() { Id = "doc", Pages = [new() { Id = "page", Nodes = [new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B", X = 200 }] }, new() { Id = "other" }] };
DesignDocument Clone(DesignDocument d) => DocumentJson.Load(DocumentJson.Save(d));
EditorSession Editor() => new(Document());

Test("shared gesture emits one transaction after all previews", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.Select(e.Document.Find("a"));
    e.BeginInteraction("Drag"); for (var i = 0; i < 60; i++) { e.Primary!.X++; e.Preview(false); } e.CommitInteraction();
    Check(h.Commits.Count == 1 && DocumentJson.Load(h.Commits[0].Before).Find("a")!.X == 0 && DocumentJson.Load(h.Commits[0].After).Find("a")!.X == 60);
});
Test("read-only guard rejects before invoking mutation", () => {
    var e = Editor(); var h = new History { Editable = false }; e.AttachSharedHistory(h); var invoked = false;
    Throws<InvalidOperationException>(() => e.Edit("Draw", () => invoked = true)); Check(!invoked && !e.IsInteracting && h.Commits.Count == 0);
});
Test("failed shared submission retains rollback snapshot until cancellation", () => {
    var e = Editor(); e.AttachSharedHistory(new History { Fail = true }); e.Select(e.Document.Find("a"));
    Throws<InvalidOperationException>(() => e.UpdateSelection("Move", n => n.X = 50));
    Check(!e.IsInteracting && e.Primary!.X == 0);
});
Test("remote application preserves viewport page selection and row expansion", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.Document.Find("a")!.Expanded = false; e.Select(e.Document.Find("a"));
    e.Viewport.ZoomAt(2, new Vec2(30, 40)); e.Viewport.Pan = new(10, 20);
    var d = Clone(e.Document); d.Find("b")!.Y = 99; d.Find("a")!.Expanded = true; e.ApplySharedDocument(d);
    Check(e.Page.Id == "page" && e.Primary!.Id == "a" && !e.Primary.Expanded && e.Viewport.Zoom == 2 && e.Viewport.Pan == new Vec2(10, 20) && h.Commits.Count == 0);
});
Test("remote application cannot cross active local transaction", () => {
    var e = Editor(); e.AttachSharedHistory(new History()); var d = Clone(e.Document); d.Find("a")!.X = 70;
    e.BeginInteraction("Drag"); e.Document.Find("b")!.X = 230;
    Throws<InvalidOperationException>(() => e.ApplySharedDocument(d)); Check(e.Document.Find("a")!.X == 0 && e.IsInteracting); e.CancelInteraction();
});
Test("undo during pointer transaction cancels locally before remote history", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.BeginInteraction("Drag"); e.Document.Find("a")!.X = 80; e.Undo();
    Check(h.UndoCalls == 0 && !e.IsInteracting && e.Document.Find("a")!.X == 0); e.Undo(); Check(h.UndoCalls == 1);
});
Test("local selection and page changes do not submit document transactions", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.Select(e.Document.Find("a")); e.SetPage("other"); e.Viewport.ZoomAt(3, Vec2.Zero); e.Notify(EditorChangeKind.Viewport);
    Check(h.Commits.Count == 0);
});
Test("opening unrelated file requires leaving collaboration", () => {
    var e = Editor(); e.AttachSharedHistory(new History()); Throws<InvalidOperationException>(() => e.Load(Document()));
    e.DetachSharedHistory(); e.Load(Document()); Check(e.SharedHistory is null);
});
Test("remote deletion clears obsolete selection safely", () => {
    var e = Editor(); e.AttachSharedHistory(new History()); e.Select(e.Document.Find("a")); var d = Clone(e.Document); d.Pages[0].Nodes.RemoveAt(0); e.ApplySharedDocument(d);
    Check(e.Primary is null && e.SelectedIds.Count == 0);
});
Test("shared version restoration is one new transaction", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); var d = Clone(e.Document); d.Find("a")!.X = 25; e.RestoreSharedVersion(d);
    Check(h.Commits.Count == 1 && h.Commits[0].Label == "Restore shared version" && e.Document.Find("a")!.X == 25);
});
Test("shared history detachment never exposes obsolete local snapshots", () => {
    var e = Editor(); e.Select(e.Document.Find("a")); e.MoveSelection(1, 0); e.AttachSharedHistory(new History()); e.DetachSharedHistory();
    Check(!e.CanUndo && !e.CanRedo && e.Document.Find("a")!.X == 1);
});
Test("queued edits against deleted entities never expose an orphaned half-node", () => {
    var initial = DocumentProjection.FromDocument(Document()); var replica = new SharedReplica(initial); var server = new TransactionEngine(initial);
    var local = DocumentProjection.ToDocument(initial); local.Find("a")!.X = 42; replica.Submit(DocumentJson.Save(local), "Local move");
    var remote = DocumentProjection.ToDocument(initial); remote.Pages[0].Nodes.RemoveAt(0);
    var deletion = new EditBatch("delete", "peer", 1, "Delete", 0, DocumentProjection.Diff(initial, DocumentProjection.FromDocument(remote, initial)));
    var commit = server.Prepare(deletion, RoomRole.Editor, "Peer"); server.Accept(commit); replica.Receive(new() { Commits = [commit] });
    Check(!replica.CanEdit && replica.PendingCount == 1 && DocumentProjection.ToDocument(replica.Visible).Find("a") is null);
    var pending = replica.NextBatch()!; replica.Receive(new() { Receipt = new(pending.Id, pending.Sequence, false, 1, "Deleted by peer") });
    Check(replica.Recovery.Count == 1 && DocumentJson.Load(replica.Recovery[0].Document).Find("a")!.X == 42);
});
Test("recovery blocks additional edits until explicitly acknowledged", () => {
    var r = new SharedReplica(DocumentProjection.FromDocument(Document())); var d = Document(); d.Find("a")!.X = 2; r.Submit(DocumentJson.Save(d), "Move");
    var pending = r.NextBatch()!; r.Receive(new() { Receipt = new(pending.Id, pending.Sequence, false, 0, "Conflict") });
    Check(!r.CanEdit); r.AcknowledgeRecovery(); Check(r.CanEdit && r.LastError is null);
});
Test("root identity cannot be replaced over the collaboration protocol", () => {
    var s = DocumentProjection.FromDocument(Document()); var key = DocumentProjection.Key("$root", "id");
    Throws<InvalidDataException>(() => new TransactionEngine(s).Prepare(new("x", "peer", 1, "Replace identity", 0, [new(key, s.Value(key), "\"other\"")]), RoomRole.Editor, "Peer"));
});
Test("invitation round trip carries credentials only in the fragment", () => {
    var address = new RoomAddress("https://example.org/service", new string('a', 32), new string('b', 43)); var link = address.Link("https://example.org/VectorSpace/?test=1");
    Check(RoomAddress.Parse(link) == address && new Uri(link).Query == "?test=1" && !new Uri(link).GetLeftPart(UriPartial.Query).Contains(address.Token));
});
foreach (var server in new[] { "http://example.org", "file:///tmp/room", "https://user:password@example.org", "https://example.org/?key=secret", "https://example.org/#fragment" })
    Test("unsafe room endpoint rejected " + server, () => Throws<ArgumentException>(() => new RoomAddress(server, new string('a', 32), new string('b', 43)).Endpoint()));
Test("loopback HTTP remains usable for actual local network tests", () => {
    Check(new RoomAddress("http://127.0.0.1:5097", new string('a', 32), new string('b', 43)).Endpoint().IsLoopback);
});

Test("retained projection matches reference and preserves unchanged storage", () => Check(ProjectionChecks.Run() == 59));

Test("shared commit rejected after an active gesture can roll back without losing selection", () => {
    var e = Editor(); var h = new History(); e.AttachSharedHistory(h); e.Select(e.Document.Find("a"));
    e.BeginInteraction("Drag"); e.Primary!.X = 99; h.Fail = true;
    Throws<InvalidOperationException>(() => e.CommitInteraction());
    Check(e.IsInteracting); e.CancelInteraction();
    Check(!e.IsInteracting && e.Primary!.X == 0 && e.Primary.Id == "a" && h.Commits.Count == 0);
});

var failures = 0;
foreach (var (name, run) in tests) { try { run(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + "\n" + e); } }
Console.WriteLine($"SHARED EDITOR RESULT: {tests.Count - failures}/{tests.Count} passed"); return failures == 0 ? 0 : 1;

sealed class History : ISharedEditorHistory
{
    public bool Editable = true, Fail;
    public int UndoCalls, RedoCalls;
    public List<(string Label, string Before, string After)> Commits { get; } = [];
    public bool CanEdit(string label) => Editable;
    public bool CanUndo => true;
    public bool CanRedo => true;
    public string UndoLabel => "Shared";
    public string RedoLabel => "Shared";
    IReadOnlyList<string> ISharedEditorHistory.History => Commits.Select(c => c.Label).ToArray();
    public void Commit(string label, string before, string after)
    { if (Fail) throw new InvalidOperationException("Simulated queue failure"); Commits.Add((label, before, after)); }
    public void Undo() => UndoCalls++;
    public void Redo() => RedoCalls++;
}
