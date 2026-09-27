using System.Text.Json;
using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Layout;
using VectorSpace.Skia;

var tests = new List<(string Name, Action Test)>();
void Test(string name, Action action) => tests.Add((name, action));
void Equal(double actual, double expected, double epsilon = .0001) { if (Math.Abs(actual - expected) > epsilon) throw new Exception($"Expected {expected}; got {actual}."); }
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception."); }
DesignNode Node(double x = 0, double y = 0, double w = 100, double h = 100) => new() { X = x, Y = y, Width = w, Height = h };
EditorSession Editor(params DesignNode[] nodes) => new(new() { Pages = [new() { Nodes = nodes.ToList() }] });

Test("affine composition and inversion", () => { var matrix = Matrix2D.Rotation(37) * Matrix2D.Translation(123, -8); var p = matrix.Inverse.Map(matrix.Map(new Vec2(40, 60))); Equal(p.X, 40); Equal(p.Y, 60); });
Test("singular matrix rejected", () => Check(!Matrix2D.Scale(0, 1).TryInvert(out _)));
Test("transformed bounds", () => { var b = Matrix2D.Rotation(90).Map(new RectD(0, 0, 100, 50)); Equal(b.Width, 50); Equal(b.Height, 100); });
Test("nested world transform", () => { var parent = Node(100, 200); var child = parent.Add(Node(10, 20)); Equal(child.WorldBounds.X, 110); Equal(child.WorldBounds.Y, 220); });
Test("rotated transform decomposition", () => { var n = Node(10, 20, 50, 60); n.Rotation = 30; var m = n.LocalMatrix * Matrix2D.Rotation(45); var expected = m.Map(new Vec2(0, 0)); NodeGeometry.SetLocalMatrix(n, m); var actual = n.LocalMatrix.Map(Vec2.Zero); Equal(actual.X, expected.X); Equal(actual.Y, expected.Y); });
Test("horizontal auto layout", () => { var frame = Node(0, 0, 300, 100); frame.Layout.Direction = LayoutDirection.Horizontal; var a = frame.Add(Node(0, 0, 40, 20)); var b = frame.Add(Node(0, 0, 60, 20)); LayoutEngine.Arrange(frame); Equal(a.X, 16); Equal(b.X, 72); });
Test("vertical auto layout", () => { var frame = Node(); frame.Layout.Direction = LayoutDirection.Vertical; var a = frame.Add(Node(0, 0, 20, 20)); var b = frame.Add(Node(0, 0, 20, 30)); LayoutEngine.Arrange(frame); Equal(a.Y, 16); Equal(b.Y, 52); });
Test("hug sizing", () => { var f = Node(); f.Layout = new() { Direction = LayoutDirection.Horizontal, HugWidth = true }; f.Add(Node(0, 0, 40, 20)); f.Add(Node(0, 0, 60, 20)); LayoutEngine.Arrange(f); Equal(f.Width, 148); });
Test("fill sizing", () => { var f = Node(0, 0, 300, 100); f.Layout.Direction = LayoutDirection.Horizontal; f.Add(Node(0, 0, 40, 20)); var c = f.Add(Node()); c.FillWidth = true; LayoutEngine.Arrange(f); Equal(c.Width, 212); });
Test("end constraint", () => { var f = Node(0, 0, 300, 100); var c = f.Add(Node(200, 10, 50, 20)); c.HorizontalConstraint = AxisConstraint.End; LayoutEngine.Resize(f, 400, 100); Equal(c.X, 300); });
Test("stretch constraint", () => { var f = Node(0, 0, 300, 100); var c = f.Add(Node(20, 10, 260, 20)); c.HorizontalConstraint = AxisConstraint.Stretch; LayoutEngine.Resize(f, 400, 100); Equal(c.Width, 360); });
Test("scale constraint", () => { var f = Node(0, 0, 300, 100); var c = f.Add(Node(30, 10, 60, 20)); c.HorizontalConstraint = AxisConstraint.Scale; LayoutEngine.Resize(f, 600, 100); Equal(c.X, 60); Equal(c.Width, 120); });
Test("snapping threshold", () => { var snap = SnapEngine.Snap(new(101, 50, 40, 40), [new(100, 0, 100, 20)], 2); Equal(snap.Correction.X, -1); Check(snap.Lines.Count > 0); });
Test("guide snapping", () => { var snap = SnapEngine.Snap(new(101, 50, 40, 40), [], 3, [new() { Position = 100 }]); Equal(snap.Correction.X, -1); });
Test("JSON round trip parent links", () => { var p = Node(); p.Add(Node()); var d = new DesignDocument { Pages = [new() { Nodes = [p] }] }; var restored = DocumentJson.Load(DocumentJson.Save(d)); Check(restored.Pages[0].Nodes[0].Children[0].Parent == restored.Pages[0].Nodes[0]); });
Test("duplicate identifiers rejected", () => { var a = Node(); var b = Node(); b.Id = a.Id; Throws(() => Editor(a, b)); });
Test("nonfinite geometry rejected", () => { var n = Node(); n.X = double.NaN; Throws(() => Editor(n)); });
Test("future format rejected", () => Throws(() => DocumentJson.Validate(new() { FormatVersion = 999 })));
Test("empty page collection rejected", () => Throws(() => DocumentJson.Validate(new() { Pages = [] })));
Test("node clones own their children", () => { var n = Node(); n.Add(Node()); var c = DocumentJson.CloneNode(n, true); Check(c.Id != n.Id && c.Children[0].Id != n.Children[0].Id && c.Children[0].Parent == c); });
Test("undo and redo property edit", () => { var n = Node(); var e = Editor(n); e.Select(n); e.UpdateSelection("Move", x => x.X = 77); e.Undo(); Equal(e.Primary!.X, 0); e.Redo(); Equal(e.Primary!.X, 77); });
Test("gesture creates one undo item", () => { var n = Node(); var e = Editor(n); e.Select(n); e.BeginInteraction("Drag"); for (var i = 0; i < 20; i++) { n.X++; e.Preview(); } e.CommitInteraction(); Equal(e.History.Count, 1); e.Undo(); Equal(e.Primary!.X, 0); });
Test("gesture cancellation", () => { var n = Node(); var e = Editor(n); e.Select(n); e.BeginInteraction("Drag"); n.X = 100; e.CancelInteraction(); Equal(e.Primary!.X, 0); Check(!e.CanUndo); });
Test("failed edits roll back", () => { var n = Node(); var e = Editor(n); e.Select(n); Throws(() => e.Edit("Fail", () => { n.X = 99; throw new Exception(); })); Equal(e.Primary!.X, 0); });
Test("no-op creates no history", () => { var e = Editor(Node()); e.Edit("Nothing", () => { }); Check(!e.CanUndo); });
Test("new edit clears redo", () => { var n = Node(); var e = Editor(n); e.Select(n); e.MoveSelection(5, 0); e.Undo(); e.MoveSelection(3, 0); Check(!e.CanRedo); });
Test("save state restored by undo", () => { var n = Node(); var e = Editor(n); e.Select(n); e.MoveSelection(1, 0); Check(e.IsDirty); e.Undo(); Check(!e.IsDirty); });
Test("group preserves world positions", () => { var a = Node(10, 20); var b = Node(150, 50); var e = Editor(a, b); e.Select([a.Id, b.Id]); e.GroupSelection(); Equal(e.Page.Nodes.Count, 1); Equal(e.Primary!.Children[0].WorldBounds.X, 10); Equal(e.Primary.Children[1].WorldBounds.Y, 50); });
Test("ungroup preserves rotated coordinates", () => { var g = Node(20, 30, 200, 100); g.Kind = NodeKind.Group; g.Rotation = 30; var c = g.Add(Node(10, 20)); var expected = c.WorldMatrix.Map(Vec2.Zero); var e = Editor(g); e.Select(g); e.UngroupSelection(); var actual = e.Primary!.WorldMatrix.Map(Vec2.Zero); Equal(actual.X, expected.X); Equal(actual.Y, expected.Y); });
Test("selection roots exclude descendants", () => { var p = Node(); var c = p.Add(Node()); var e = Editor(p); e.Select([p.Id, c.Id]); Equal(e.SelectionRoots.Count, 1); });
Test("locked layer cannot be deleted", () => { var n = Node(); n.Locked = true; var e = Editor(n); e.Select(n); e.DeleteSelection(); Equal(e.Page.Nodes.Count, 1); });
Test("clipboard regenerates ids", () => { var n = Node(); var e = Editor(n); e.Select(n); e.Paste(e.CopySelection()); Equal(e.Page.Nodes.Count, 2); Check(e.Page.Nodes[0].Id != e.Page.Nodes[1].Id); });
Test("last page cannot be deleted", () => { var e = Editor(); e.DeletePage(e.Page.Id); Equal(e.Document.Pages.Count, 1); });
Test("page creation undo restores current page", () => { var e = Editor(); var id = e.Page.Id; e.AddPage(); e.Undo(); Check(e.Page.Id == id); });
Test("component instance linkage", () => { var c = Node(); c.Kind = NodeKind.Component; c.Add(new() { Kind = NodeKind.Text, Text = "Before" }); var e = Editor(c); var i = ComponentService.InsertInstance(e, c, new(300, 0)); c.Children[0].Text = "After"; ComponentService.Synchronize(e.Document); Check(i.Children[0].Text == "After" && i.ComponentId == c.Id); });
Test("component text override survives sync", () => { var c = Node(); c.Kind = NodeKind.Component; c.Add(new() { Kind = NodeKind.Text }); var e = Editor(c); var i = ComponentService.InsertInstance(e, c, new(300, 0)); ComponentService.SetOverride(i.Children[0], text: "Override"); ComponentService.Synchronize(e.Document); Check(i.Children[0].Text == "Override"); });
Test("zoom preserves screen anchor", () => { var v = new Viewport(); var world = v.ScreenToWorld(new(250, 300)); v.ZoomAt(2.5, new(250, 300)); Equal(v.WorldToScreen(world).X, 250); Equal(v.WorldToScreen(world).Y, 300); });
Test("zoom clamps extreme values", () => { var v = new Viewport(); v.ZoomAt(1e9, new()); Equal(v.Zoom, 64); v.ZoomAt(0, new()); Equal(v.Zoom, .02); });
Test("SVG safe text escaping", () => { var n = Node(); n.Kind = NodeKind.Text; n.Text = "<script>& text"; var svg = SvgFormat.Export([n], n.WorldBounds); Check(svg.Contains("&lt;script&gt;&amp; text") && !svg.Contains("<script>")); });
Test("SVG import primitives", () => { var r = SvgFormat.Import("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect x='10' y='20' width='30' height='40' fill='#ff0000'/></svg>"); Equal(r.Document.Pages[0].Nodes[0].Children[0].X, 10); });
Test("SVG blocks DTD", () => Throws(() => SvgFormat.Import("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg>&x;</svg>")));
Test("SVG reports unsupported external content", () => { var r = SvgFormat.Import("<svg><script>alert(1)</script><image href='https://example.com/x'/></svg>"); Equal(r.Warnings.Count, 2); });
Test("SVG transform order", () => { var p = SvgFormat.ParseTransform("translate(10,20) scale(2)").Map(new Vec2(5, 5)); Equal(p.X, 20); Equal(p.Y, 30); });
Test("Skia hit test ellipse precision", () => { using var renderer = new SceneRenderer(); var n = Node(); n.Kind = NodeKind.Ellipse; Check(renderer.HitTest([n], new(1, 1)) is null); Check(renderer.HitTest([n], new(50, 50)) == n); });
Test("Skia clipping hit test", () => { using var r = new SceneRenderer(); var p = Node(0, 0, 20, 20); p.ClipContent = true; p.Add(Node(50, 50)); Check(r.HitTest([p], new(60, 60), true) is null); });
Test("Skia PNG export writes pixels", () => { using var r = new SceneRenderer(); var n = Node(); n.Fill = "#FF0000"; var bytes = r.ExportPng([n], n.WorldBounds); using var image = SKBitmap.Decode(bytes); Check(image.Width == 100 && image.GetPixel(50, 50).Red > 240); });
Test("Skia export size limit", () => { using var r = new SceneRenderer(); Throws(() => r.ExportPng([Node()], new(0, 0, 20000, 20000))); });
Test("Skia Boolean union", () => { using var r = new SceneRenderer(); var a = Node(); var b = Node(50, 0); var e = Editor(a, b); e.Select([a.Id, b.Id]); BooleanOperations.Apply(e, r, BooleanOperation.Union); Equal(e.Page.Nodes.Count, 1); Equal(e.Primary!.Width, 150); e.Undo(); Equal(e.Page.Nodes.Count, 2); });

Test("nested selection nudges once", () => { var parent = Node(); var child = parent.Add(Node(10, 10)); var e = Editor(parent); e.Select([parent.Id, child.Id]); e.MoveSelection(1, 0); Equal(parent.X, 1); Equal(child.X, 10); });
Test("clipboard captures world placement", () => { var parent = Node(100, 200); var child = parent.Add(Node(10, 20)); var e = Editor(parent); e.Select(child); e.Paste(e.CopySelection()); Equal(e.Primary!.X, 134); Equal(e.Primary.Y, 244); });
Test("component creation preserves leaf content", () => { var n = Node(30, 40); n.Fill = "#FF0000"; var e = Editor(n); e.Select(n); ComponentService.MakeComponent(e); Check(e.Primary!.Kind == NodeKind.Component); Equal(e.Primary.Children.Count, 1); Check(e.Primary.Children[0].Fill == "#FF0000"); Equal(e.Primary.Children[0].WorldBounds.X, 30); e.Undo(); Check(e.Primary!.Kind == NodeKind.Rectangle); });
Test("source edits synchronize instances atomically", () => { var c = Node(); c.Kind = NodeKind.Component; c.Add(new() { Kind = NodeKind.Text, Text = "Before" }); var e = Editor(c); var i = ComponentService.InsertInstance(e, c, new(300, 0)); var instanceId = i.Id; e.Select(c.Children[0]); e.UpdateSelection("Edit source", n => n.Text = "After"); Check(e.Document.Find(instanceId)!.Children[0].Text == "After"); e.Undo(); Check(e.Document.Find(instanceId)!.Children[0].Text == "Before"); });
Test("sample is valid and renderable", () => { var document = SampleDocument.Create(); DocumentJson.Validate(document); using var renderer = new SceneRenderer(); var frame = document.Pages[0].Nodes[0]; var png = renderer.ExportPng([frame], frame.WorldBounds, .25); using var bitmap = SKBitmap.Decode(png); Check(bitmap.Width == 260 && bitmap.Height == 205); });

ParityTests.Register(Test);

var failed = 0;
foreach (var (name, test) in tests) { try { test(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + "\n" + ex); } }
Console.WriteLine($"RESULT: {tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
