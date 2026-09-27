using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Layout;

namespace VectorSpace.Editing;

public enum EditorTool { Move, Scale, Frame, Section, Rectangle, Ellipse, Line, Arrow, Polygon, Star, Pen, Pencil, Text, Hand, Comment, Slice }
public enum EditorChangeKind { Document, Selection, Preview, Viewport, Tool }
public sealed class EditorChangedEventArgs(EditorChangeKind kind, string label = "") : EventArgs
{
    public EditorChangeKind Kind { get; } = kind;
    public string Label { get; } = label;
}
public sealed class Viewport
{
    public double Zoom { get; private set; } = 1;
    public Vec2 Pan { get; set; }
    public Vec2 WorldToScreen(Vec2 p) => p * Zoom + Pan;
    public Vec2 ScreenToWorld(Vec2 p) => (p - Pan) / Zoom;
    public void ZoomAt(double zoom, Vec2 screenAnchor)
    {
        var world = ScreenToWorld(screenAnchor); Zoom = Numbers.Clamp(zoom, .02, 64); Pan = screenAnchor - world * Zoom;
    }
    public void Fit(RectD bounds, double width, double height, double padding = 64)
    {
        Zoom = Math.Clamp(Math.Min(Math.Max(1, width - 2 * padding) / Math.Max(1, bounds.Width), Math.Max(1, height - 2 * padding) / Math.Max(1, bounds.Height)), .02, 4);
        Pan = new Vec2(width / 2, height / 2) - bounds.Center * Zoom;
    }
}

/// <summary>UI-independent editor state. A pointer gesture is one atomic, cancellable history entry.</summary>
public sealed class EditorSession
{
    private sealed record Snapshot(string Json, string PageId, string[] Selection);
    private sealed record HistoryEntry(string Label, Snapshot Before, Snapshot After);
    private readonly List<HistoryEntry> _undo = [];
    private readonly Stack<HistoryEntry> _redo = [];
    private readonly HashSet<string> _selected = [];
    private Snapshot? _before;
    private string _interactionLabel = "Edit";
    private string _savedJson;
    private EditorTool _tool;
    public event EventHandler<EditorChangedEventArgs>? Changed;
    public DesignDocument Document { get; private set; }
    public DesignPage Page { get; private set; }
    public Viewport Viewport { get; } = new();
    public bool SnapEnabled { get; set; } = true;
    public bool GridVisible { get; set; }
    public bool RulersVisible { get; set; }
    public bool OutlinesVisible { get; set; }
    public bool IsDirty { get; private set; }
    public bool IsInteracting => _before is not null;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string UndoLabel => _undo.LastOrDefault()?.Label ?? "";
    public string RedoLabel => _redo.TryPeek(out var item) ? item.Label : "";
    public IReadOnlyList<string> History => _undo.Select(e => e.Label).ToArray();
    public IReadOnlySet<string> SelectedIds => _selected;
    public IReadOnlyList<DesignNode> Selection => Page.AllNodes().Where(n => _selected.Contains(n.Id)).ToArray();
    public IReadOnlyList<DesignNode> SelectionRoots => Selection.Where(n => !Ancestors(n).Any(a => _selected.Contains(a.Id))).ToArray();
    public DesignNode? Primary => Selection.LastOrDefault();
    public EditorTool Tool { get => _tool; set { if (_tool == value) return; _tool = value; Notify(EditorChangeKind.Tool); } }
    public EditorSession(DesignDocument document)
    {
        DocumentJson.Validate(document); document.RebuildParents(); Document = document; Page = document.Pages[0]; _savedJson = DocumentJson.Save(document);
    }
    public void Load(DesignDocument document)
    {
        DocumentJson.Validate(document); document.RebuildParents(); Document = document; Page = document.Pages[0];
        _before = null; _selected.Clear(); _undo.Clear(); _redo.Clear(); _savedJson = DocumentJson.Save(document); IsDirty = false; Notify(EditorChangeKind.Document, "Open document");
    }
    public void SetPage(string id)
    {
        if (IsInteracting) CancelInteraction();
        var page = Document.Pages.FirstOrDefault(p => p.Id == id); if (page is null) return;
        Page = page; _selected.Clear(); Notify(EditorChangeKind.Document, "Switch page");
    }
    public void Select(IEnumerable<string> ids, bool toggle = false)
    {
        var existing = Page.AllNodes().Select(n => n.Id).ToHashSet();
        if (!toggle) _selected.Clear();
        foreach (var id in ids.Where(existing.Contains)) if (!toggle || !_selected.Remove(id)) _selected.Add(id);
        Notify(EditorChangeKind.Selection);
    }
    public void Select(DesignNode? node, bool toggle = false) => Select(node is null ? [] : [node.Id], toggle);
    public void SelectAll() => Select(Page.Nodes.Where(n => n.Visible && !n.Locked).Select(n => n.Id));
    public RectD SelectionBounds()
    {
        var nodes = SelectionRoots; return nodes.Count == 0 ? default : nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union);
    }
    public void Notify(EditorChangeKind kind, string label = "") => Changed?.Invoke(this, new(kind, label));
    public void Preview()
    {
        LayoutEngine.Arrange(Page.Nodes); Notify(EditorChangeKind.Preview);
    }
    public void BeginInteraction(string label)
    {
        if (_before is not null) throw new InvalidOperationException("An edit transaction is already active.");
        _before = Capture(); _interactionLabel = label;
    }
    public void CommitInteraction()
    {
        if (_before is null) return;
        LayoutEngine.Arrange(Page.Nodes);
        var before = _before; _before = null; var after = Capture();
        if (before.Json != after.Json)
        {
            _undo.Add(new(_interactionLabel, before, after)); _redo.Clear();
            while (_undo.Count > 150 || (_undo.Count > 1 && _undo.Sum(x => (long)x.Before.Json.Length + x.After.Json.Length) > 32 * 1024 * 1024)) _undo.RemoveAt(0);
            IsDirty = after.Json != _savedJson;
        }
        Notify(EditorChangeKind.Document, _interactionLabel);
    }
    public void CancelInteraction()
    {
        if (_before is null) return; var before = _before; _before = null; Restore(before); Notify(EditorChangeKind.Document, "Cancel edit");
    }
    public void Edit(string label, Action action)
    {
        BeginInteraction(label);
        try { action(); CommitInteraction(); }
        catch { CancelInteraction(); throw; }
    }
    public void Undo()
    {
        if (IsInteracting) { CancelInteraction(); return; }
        if (_undo.Count == 0) return; var entry = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); _redo.Push(entry); Restore(entry.Before); Notify(EditorChangeKind.Document, "Undo " + entry.Label);
    }
    public void Redo()
    {
        if (IsInteracting || !_redo.TryPop(out var entry)) return; _undo.Add(entry); Restore(entry.After); Notify(EditorChangeKind.Document, "Redo " + entry.Label);
    }
    public void MarkSaved(string? json = null)
    {
        _savedJson = json ?? DocumentJson.Save(Document); IsDirty = DocumentJson.Save(Document) != _savedJson; Notify(EditorChangeKind.Selection);
    }
    public void AddNode(DesignNode node, DesignNode? parent = null)
    {
        node.Parent = parent; (parent?.Children ?? Page.Nodes).Add(node);
    }
    public void RemoveNode(DesignNode node) => (node.Parent?.Children ?? Page.Nodes).Remove(node);
    public void DeleteSelection()
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray(); if (nodes.Length == 0) return;
        Edit("Delete layers", () => { foreach (var n in nodes) RemoveNode(n); _selected.Clear(); });
    }
    public void UpdateSelection(string label, Action<DesignNode> update)
    {
        var nodes = Selection.Where(n => !n.IsEffectivelyLocked).ToArray(); if (nodes.Length == 0) return;
        Edit(label, () => { foreach (var n in nodes) update(n); });
    }
    public void MoveSelection(double x, double y) => UpdateSelection("Move layers", n => { n.X += x; n.Y += y; });
    public void DuplicateSelection(double offset = 24)
    {
        var nodes = SelectionRoots.ToArray(); if (nodes.Length == 0) return;
        Edit("Duplicate layers", () => DuplicateInTransaction(nodes, offset));
    }
    public void DuplicateInTransaction(IEnumerable<DesignNode> originals, double offset = 0)
    {
        var newIds = new List<string>();
        foreach (var node in originals.ToArray())
        {
            var clone = DocumentJson.CloneNode(node, true); clone.X += offset; clone.Y += offset; AddNode(clone, node.Parent); newIds.Add(clone.Id);
        }
        _selected.Clear(); _selected.UnionWith(newIds);
    }
    public string CopySelection() => DocumentJson.SaveNodes(SelectionRoots);
    public void Paste(string json)
    {
        var nodes = DocumentJson.LoadNodes(json); if (nodes.Count == 0) return;
        Edit("Paste layers", () => { _selected.Clear(); foreach (var n in nodes) { n.X += 24; n.Y += 24; AddNode(n); _selected.Add(n.Id); } });
    }
    public void GroupSelection(bool asFrame = false)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray(); if (nodes.Length == 0) return;
        var parent = nodes[0].Parent; if (nodes.Any(n => n.Parent != parent)) return;
        Edit(asFrame ? "Frame selection" : "Group selection", () =>
        {
            var bounds = nodes.Select(n => n.LocalMatrix.Map(n.LocalBounds)).Aggregate(RectD.Union);
            var group = new DesignNode { Kind = asFrame ? NodeKind.Frame : NodeKind.Group, Name = asFrame ? "Frame" : "Group", X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Fills = [], ClipContent = false };
            var siblings = parent?.Children ?? Page.Nodes; var index = nodes.Min(n => siblings.IndexOf(n));
            foreach (var n in nodes) { siblings.Remove(n); n.X -= bounds.X; n.Y -= bounds.Y; group.Add(n); }
            group.Parent = parent; siblings.Insert(index, group); _selected.Clear(); _selected.Add(group.Id);
        });
    }
    public void UngroupSelection()
    {
        var groups = SelectionRoots.Where(n => n.IsContainer && !n.IsEffectivelyLocked).ToArray(); if (groups.Length == 0) return;
        Edit("Ungroup layers", () =>
        {
            _selected.Clear();
            foreach (var group in groups)
            {
                var siblings = group.Parent?.Children ?? Page.Nodes; var index = siblings.IndexOf(group);
                foreach (var child in group.Children.ToArray())
                {
                    var matrix = child.LocalMatrix * group.LocalMatrix; NodeGeometry.SetLocalMatrix(child, matrix); child.Parent = group.Parent; siblings.Insert(index++, child); _selected.Add(child.Id);
                }
                group.Children.Clear(); siblings.Remove(group);
            }
        });
    }
    public void Reorder(int direction, bool extreme = false)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray(); if (nodes.Length == 0) return;
        Edit(direction > 0 ? "Bring forward" : "Send backward", () =>
        {
            foreach (var node in direction > 0 ? nodes.Reverse() : nodes)
            {
                var list = node.Parent?.Children ?? Page.Nodes; var index = list.IndexOf(node);
                list.RemoveAt(index); list.Insert(extreme ? (direction > 0 ? list.Count : 0) : Math.Clamp(index + direction, 0, list.Count), node);
            }
        });
    }
    public void Align(string alignment)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).ToArray(); if (nodes.Length == 0) return;
        var box = nodes.Length == 1 && nodes[0].Parent is { } parent ? parent.WorldBounds : nodes.Select(n => n.WorldBounds).Aggregate(RectD.Union);
        Edit("Align " + alignment, () =>
        {
            foreach (var node in nodes)
            {
                var b = node.WorldBounds;
                var delta = alignment switch { "left" => new Vec2(box.X - b.X, 0), "center" => new Vec2(box.Center.X - b.Center.X, 0), "right" => new Vec2(box.Right - b.Right, 0), "top" => new Vec2(0, box.Y - b.Y), "middle" => new Vec2(0, box.Center.Y - b.Center.Y), "bottom" => new Vec2(0, box.Bottom - b.Bottom), _ => Vec2.Zero };
                var inverse = node.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity; var localDelta = inverse.Map(delta) - inverse.Map(Vec2.Zero); node.X += localDelta.X; node.Y += localDelta.Y;
            }
        });
    }
    public void Distribute(bool horizontal)
    {
        var nodes = SelectionRoots.Where(n => !n.IsEffectivelyLocked).OrderBy(n => horizontal ? n.WorldBounds.X : n.WorldBounds.Y).ToArray(); if (nodes.Length < 3) return;
        Edit(horizontal ? "Distribute horizontal spacing" : "Distribute vertical spacing", () =>
        {
            var first = nodes[0].WorldBounds; var last = nodes[^1].WorldBounds;
            var space = (horizontal ? last.Right - first.X - nodes.Sum(n => n.WorldBounds.Width) : last.Bottom - first.Y - nodes.Sum(n => n.WorldBounds.Height)) / (nodes.Length - 1);
            var cursor = horizontal ? first.X : first.Y;
            foreach (var n in nodes)
            {
                var bounds = n.WorldBounds; var delta = horizontal ? new Vec2(cursor - bounds.X, 0) : new Vec2(0, cursor - bounds.Y);
                var inverse = n.Parent?.WorldMatrix.Inverse ?? Matrix2D.Identity; var local = inverse.Map(delta) - inverse.Map(Vec2.Zero); n.X += local.X; n.Y += local.Y; cursor += (horizontal ? bounds.Width : bounds.Height) + space;
            }
        });
    }
    public void AddPage()
    {
        Edit("Add page", () => { var p = new DesignPage { Name = "Page " + (Document.Pages.Count + 1) }; Document.Pages.Add(p); Page = p; _selected.Clear(); });
    }
    public void DeletePage(string id)
    {
        if (Document.Pages.Count < 2) return;
        Edit("Delete page", () => { Document.Pages.RemoveAll(p => p.Id == id); Document.Comments.RemoveAll(c => c.PageId == id); if (Page.Id == id) Page = Document.Pages[0]; _selected.Clear(); });
    }
    private Snapshot Capture() => new(DocumentJson.Save(Document), Page.Id, _selected.ToArray());
    private void Restore(Snapshot state)
    {
        Document = DocumentJson.Load(state.Json); Page = Document.Pages.FirstOrDefault(p => p.Id == state.PageId) ?? Document.Pages[0];
        _selected.Clear(); _selected.UnionWith(state.Selection.Where(id => Page.AllNodes().Any(n => n.Id == id))); IsDirty = state.Json != _savedJson;
    }
    private static IEnumerable<DesignNode> Ancestors(DesignNode node) { for (var p = node.Parent; p is not null; p = p.Parent) yield return p; }
}
