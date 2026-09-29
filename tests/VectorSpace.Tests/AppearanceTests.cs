using System.Xml.Linq;
using System.Text.Json;
using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;
using VectorSpace.Prototyping;
using VectorSpace.Skia;

internal static class AppearanceTests
{
    private static void Check(bool value, string message = "Appearance assertion failed") { if (!value) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance = .001) { if (Math.Abs(actual - expected) > tolerance) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Reject(Action action) { try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException) { return; } throw new Exception("Expected rejection"); }
    private static DesignNode Node(params FillStyle[] fills) => new() { Width = 100, Height = 100, Fills = fills.ToList() };
    private static DesignDocument Document(DesignNode node) => new() { Pages = [new() { Nodes = [node] }] };
    private static SKBitmap Render(SceneRenderer r, DesignNode node, RectD? bounds = null) => SKBitmap.Decode(r.ExportPng([node], bounds ?? new RectD(0, 0, 100, 100)));
    private static byte[] Raster(int w = 20, int h = 10, bool alternate = false)
    {
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) bitmap.SetPixel(x, y, alternate ? SKColors.Green : x < w / 2 ? SKColors.Red : SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
    private static FillStyle Image(ImageScaleMode mode = ImageScaleMode.Fill) => new() { Kind = FillKind.Image, ImageData = RasterImageCodec.Import(Raster()).DataUri, ImageMode = mode };
    private static FillStyle Gradient() => new() { Kind = FillKind.LinearGradient, Start = new(0, 0), End = new(1, 0), Stops = [new() { Offset = 0, Color = "#FF0000" }, new() { Offset = 1, Color = "#0000FF" }] };

    public static void Register(Action<string, Action> test)
    {
        test("appearance playground validates, roundtrips and renders all materials", () =>
        {
            var doc = DocumentJson.Load(DocumentJson.Save(AppearanceSample.Create()));
            using var r = new SceneRenderer(); var f = doc.Pages[0].Nodes[0]; using var data = SKBitmap.Decode(r.ExportPng([f], f.LocalBounds, .5));
            Check(data.Width == 530 && data.Height == 400 && r.Images.DecodeCount == 1);
        });
        test("image fill and fit map aspect ratios without distortion", () =>
        {
            var f = Image(); var m = ImagePlacement.Calculate(f, 100, 100, 20, 10); Near(m.M11, 10); Near(m.Map(Vec2.Zero).X, -50);
            f.ImageMode = ImageScaleMode.Fit; m = ImagePlacement.Calculate(f, 100, 100, 20, 10); Near(m.M11, 5); Near(m.Map(Vec2.Zero).Y, 25);
        });
        test("image crop applies normalized translation after scale and rotation", () =>
        {
            var f = Image(ImageScaleMode.Crop); f.ImageScale = 2; f.ImageOffset = new(.25, -.1); f.ImageRotation = 90;
            var m = ImagePlacement.Calculate(f, 100, 100, 20, 10); var p = m.Map(new Vec2(10, 5)); Near(p.X, 75); Near(p.Y, 40); Check(m.TryInvert(out _));
        });
        test("rotated fill covers every destination corner", () =>
        {
            var f = Image(); f.ImageRotation = 37; var inverse = ImagePlacement.Calculate(f, 123, 85, 20, 10).Inverse;
            foreach (var p in new[] { new Vec2(0, 0), new(123, 0), new(123, 85), new(0, 85) })
            { var q = inverse.Map(p); Check(q.X >= -.001 && q.X <= 20.001 && q.Y >= -.001 && q.Y <= 10.001); }
        });
        test("image placement rejects singular and nonfinite parameters", () =>
        { var f = Image(); f.ImageScale = 0; Reject(() => ImagePlacement.Calculate(f, 100, 100, 20, 10)); f.ImageScale = 1; Reject(() => ImagePlacement.Calculate(f, double.NaN, 100, 20, 10)); });
        test("image imports are normalized bounded PNGs", () =>
        {
            var image = RasterImageCodec.Import(Raster()); Check(image.Width == 20 && image.Height == 10); Check(EmbeddedImage.Validate(image.DataUri) == "image/png");
            Check(ImageHeader.Read(EmbeddedImage.Decode(image.DataUri).Bytes) == (20, 10));
        });
        foreach (var format in new[] { SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Webp })
            test("raster codec imports " + format + " and normalizes its MIME", () =>
            {
                using var bitmap = SKBitmap.Decode(Raster()); using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(format, 90);
                var imported = RasterImageCodec.Import(encoded.ToArray()); Check(imported.Width == 20 && imported.Height == 10 && imported.DataUri.StartsWith("data:image/png;base64,"));
            });
        for (ushort orientation = 1; orientation <= 8; orientation++)
        {
            var code = orientation;
            test("JPEG orientation " + code + " is normalized before image placement", () =>
            {
                using var bitmap = SKBitmap.Decode(Raster(80, 40)); using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
                // EXIF APP1 containing one little-endian TIFF orientation tag.
                byte[] exif = [0xff, 0xe1, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
                    1, 0, 18, 1, 3, 0, 1, 0, 0, 0, (byte)code, 0, 0, 0, 0, 0, 0, 0];
                var bytes = encoded.ToArray(); byte[] tagged = [.. bytes.AsSpan(0, 2), .. exif, .. bytes.AsSpan(2)];
                var imported = RasterImageCodec.Import(tagged); var swapped = code >= 5;
                Check(imported.Width == (swapped ? 40 : 80) && imported.Height == (swapped ? 80 : 40));
                using var normalized = SKBitmap.Decode(EmbeddedImage.Decode(imported.DataUri).Bytes);
                var color = swapped ? normalized.GetPixel(20, 10) : normalized.GetPixel(10, 20);
                Check(code is 1 or 4 or 5 or 6 ? color.Red > 230 && color.Blue < 20 : color.Blue > 230 && color.Red < 20);
            });
        }
        test("embedded images reject external executable and mismatched sources", () =>
        {
            foreach (var s in new[] { "https://example.com/x.png", "file:///etc/passwd", "data:image/svg+xml;base64,PHN2Zy8+", "data:image/png;base64,%%%%" }) Reject(() => EmbeddedImage.Validate(s));
            Reject(() => EmbeddedImage.Validate(RasterImageCodec.Import(Raster()).DataUri.Replace("image/png", "image/jpeg")));
        });
        test("image header rejects pixel bombs before allocation", () =>
        { var b = Raster(); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(16), 100000); Reject(() => ImageHeader.Read(b)); Reject(() => RasterImageCodec.Decode(b)); });
        test("image codec rejects damaged and empty payloads", () => { Reject(() => RasterImageCodec.Import([])); Reject(() => RasterImageCodec.Import(Raster()[..40])); });
        test("decoded image cache shares cloned payloads and remains bounded", () =>
        {
            using var cache = new ImageAssetCache { Capacity = 1 }; var text = Image().ImageData!;
            var image = cache.Get(text); Check(image is not null); var second = new string(text.AsSpan()); Check(ReferenceEquals(cache.Get(second), image)); Check(cache.DecodeCount == 1);
            cache.Get(RasterImageCodec.Import(Raster(alternate: true)).DataUri); Check(cache.Count == 1 && cache.DecodedBytes == 800);
            cache.Get(text); Check(cache.DecodeCount == 3); cache.Clear(); Check(cache.Count == 0 && cache.DecodedBytes == 0);
        });
        test("decoded cache enforces its byte budget and negative caches corruption", () =>
        {
            using var cache = new ImageAssetCache { ByteBudget = 100 }; Check(cache.Get(Image().ImageData!) is null); Check(cache.DecodedBytes == 0);
            Check(cache.Get("bad") is null); var count = cache.Count; cache.Get("bad"); Check(cache.Count == count);
        });
        test("image cache budget changes evict immediately and allow a previously oversized image", () =>
        {
            using var c = new ImageAssetCache(); var red = Image().ImageData!; var green = RasterImageCodec.Import(Raster(alternate: true)).DataUri;
            c.Get(red); c.Get(green); Check(c.DecodedBytes == 1600); c.ByteBudget = 800; Check(c.Count == 1 && c.DecodedBytes == 800);
            c.ByteBudget = 100; Check(c.DecodedBytes == 0); Check(c.Get(red) is null && c.LastError is not null);
            c.ByteBudget = 800; Check(c.Get(red) is not null && c.LastError is null); Reject(() => c.Capacity = 0);
        });
        test("gradient matrices serialize exactly six coefficients without computed recursion", () =>
        {
            var n = Node(Gradient()); n.Fills[0].GradientTransform = Matrix2D.Rotation(35) * Matrix2D.Translation(2, 3);
            var json = DocumentJson.Save(Document(n)); using var parsed = JsonDocument.Parse(json);
            var matrix = parsed.RootElement.GetProperty("pages")[0].GetProperty("nodes")[0].GetProperty("fills")[0].GetProperty("gradientTransform");
            Check(matrix.EnumerateObject().Count() == 6 && !matrix.TryGetProperty("inverse", out _));
            Check(DocumentJson.Load(json).Pages[0].Nodes[0].Fills[0].GradientTransform == n.Fills[0].GradientTransform);
        });
        test("image fit has transparent letterboxing and no color tint", () =>
        {
            using var renderer = new SceneRenderer(); var n = Node(Image(ImageScaleMode.Fit)); n.Fills[0].Color = "#000000";
            using var b = Render(renderer, n); Check(b.GetPixel(50, 5).Alpha == 0); Check(b.GetPixel(20, 50).Red > 240); Check(b.GetPixel(80, 50).Blue > 240);
        });
        test("image fill clips correctly to nonrectangular vector geometry", () =>
        { using var r = new SceneRenderer(); var n = Node(Image()); n.Kind = NodeKind.Ellipse; using var b = Render(r, n); Check(b.GetPixel(1, 1).Alpha == 0 && b.GetPixel(50, 50).Alpha == 255); });
        test("image tile repeats source pixels", () =>
        { using var r = new SceneRenderer(); var n = Node(Image(ImageScaleMode.Tile)); using var b = Render(r, n); Check(b.GetPixel(43, 50) == b.GetPixel(63, 50)); Check(b.GetPixel(43, 50) != b.GetPixel(53, 50)); });
        test("image opacity is applied once", () =>
        { using var r = new SceneRenderer(); var f = Image(); f.Opacity = .5; using var b = Render(r, Node(f)); Near(b.GetPixel(20, 50).Alpha, 128, 1); });
        test("image saturation modifies pixels and preserves alpha", () =>
        { using var r = new SceneRenderer(); var f = Image(); f.Saturation = -1; using var b = Render(r, Node(f)); var c = b.GetPixel(20, 50); Near(c.Red, c.Green, 1); Near(c.Green, c.Blue, 1); Check(c.Alpha == 255); });
        test("image replacement invalidates decode identity without rebuilding geometry", () =>
        { using var r = new SceneRenderer(); var n = Node(Image()); using var first = Render(r, n); var builds = r.GeometryBuilds; n.Fills[0].ImageData = RasterImageCodec.Import(Raster(alternate: true)).DataUri; using var second = Render(r, n); Check(r.Images.DecodeCount == 2 && r.GeometryBuilds == builds); Check(second.GetPixel(50, 50).Green > 100); });
        test("gradient opacity is independent of fallback color alpha", () =>
        { using var r = new SceneRenderer(); var f = Gradient(); f.Opacity = .5; f.Color = "#00000000"; using var b = Render(r, Node(f)); Near(b.GetPixel(40, 40).Alpha, 128, 1); });
        test("gradient stop alpha multiplies fill opacity once", () =>
        { using var r = new SceneRenderer(); var f = Gradient(); f.Opacity = .5; foreach (var stop in f.Stops) stop.Opacity = .5; using var b = Render(r, Node(f)); Near(b.GetPixel(50, 50).Alpha, 64, 1); });
        test("gradient cache retains shaders across translation and opacity edits", () =>
        { using var r = new SceneRenderer(); var n = Node(Gradient()); using var first = Render(r, n); n.X = 2; n.Fills[0].Opacity = .4; using var second = Render(r, n); Check(r.GradientBuilds == 1); n.Fills[0].Stops[0].Color = "#00FF00"; using var third = Render(r, n); Check(r.GradientBuilds == 2); });
        test("multiple outer shadows each sample only the original source", () =>
        {
            using var r = new SceneRenderer(); var n = Node(new FillStyle() { Color = "#FFFFFF" }); n.X = n.Y = 50; n.Width = n.Height = 20;
            n.Shadows = [new() { X = -25, Y = 0, Blur = 0, Color = "#FF0000", Opacity = 1 }, new() { X = 25, Y = 0, Blur = 0, Color = "#0000FF", Opacity = 1 }];
            using var b = Render(r, n); Check(b.GetPixel(30, 55).Red == 255); Check(b.GetPixel(80, 55).Blue == 255); Check(b.GetPixel(55, 55) == SKColors.White); Check(b.GetPixel(5, 55).Alpha == 0);
        });
        test("inner shadows remain inside the source silhouette", () =>
        {
            using var r = new SceneRenderer(); var n = Node(new FillStyle() { Color = "#FFFFFF" }); n.X = n.Y = 20; n.Width = n.Height = 60;
            n.Shadows.Add(new() { Kind = EffectKind.InnerShadow, X = 10, Y = 0, Blur = 0, Color = "#FF0000", Opacity = 1 });
            using var b = Render(r, n); Check(b.GetPixel(21, 50).Red > 240 && b.GetPixel(21, 50).Green < 10); Check(b.GetPixel(50, 50) == SKColors.White); Check(b.GetPixel(19, 50).Alpha == 0);
        });
        test("inner shadows preserve translucent alpha without tinting outer shadows", () =>
        {
            using var r = new SceneRenderer(); var n = Node(new FillStyle { Color = "#80FFFFFF" }); n.X = n.Y = 20; n.Width = n.Height = 60;
            n.Shadows = [new() { Kind = EffectKind.InnerShadow, X = 10, Y = 0, Blur = 0, Color = "#FF0000", Opacity = 1 },
                new() { Kind = EffectKind.DropShadow, X = 70, Y = 0, Blur = 0, Color = "#0000FF", Opacity = 1 }];
            using var b = Render(r, n); Near(b.GetPixel(21, 50).Alpha, 128, 1); Check(b.GetPixel(21, 50).Green < 10);
            Near(b.GetPixel(50, 50).Alpha, 128, 1); Check(b.GetPixel(19, 50).Alpha == 0);
            Check(b.GetPixel(95, 50).Blue > 240 && b.GetPixel(95, 50).Red < 10);
        });
        test("effect spread changes alpha silhouette", () =>
        {
            using var r = new SceneRenderer(); var n = Node(new FillStyle() { Color = "#FFFFFF" }); n.X = n.Y = 30; n.Width = n.Height = 40;
            n.Shadows.Add(new() { X = 0, Y = 0, Blur = 0, Spread = 5, Opacity = 1, Color = "#FF0000" });
            using var b = Render(r, n); Check(b.GetPixel(27, 50).Red > 240 && b.GetPixel(24, 50).Alpha == 0);
        });
        test("layer blur processes descendants and survives conservative culling", () =>
        {
            using var r = new SceneRenderer(); var n = Node(); n.Kind = NodeKind.Group; n.Shadows.Add(new() { Kind = EffectKind.LayerBlur, Blur = 16 });
            n.Add(new() { X = 30, Y = 30, Width = 40, Height = 40, Fill = "#FF0000" });
            using var b = Render(r, n); Check(b.GetPixel(27, 50).Alpha > 0 && b.GetPixel(50, 50).Red > 240);
        });
        test("viewport culling retains filtered descendant inputs outside the view", () =>
        {
            using var r = new SceneRenderer(); using var surface = SKSurface.Create(new SKImageInfo(100, 100));
            var n = Node(); n.Kind = NodeKind.Group; n.Shadows.Add(new() { Kind = EffectKind.LayerBlur, Blur = 24 });
            n.Add(new() { X = 104, Y = 25, Width = 30, Height = 50, Fill = "#FF0000" });
            r.Draw(surface.Canvas, [n], new RectD(0, 0, 100, 100)); using var image = surface.Snapshot(); using var pixels = SKBitmap.FromImage(image);
            Check(r.RenderedNodes == 2 && pixels.GetPixel(98, 50).Alpha > 0);
        });
        test("inner-effect bounds invalidate on resize without truncating the enlarged layer", () =>
        {
            using var r = new SceneRenderer(); var n = Node(new FillStyle { Color = "#FFFFFF" }); n.Width = 20;
            n.Shadows.Add(new() { Kind = EffectKind.InnerShadow, X = -8, Y = 0, Blur = 0, Opacity = 1, Color = "#FF0000" });
            using var first = Render(r, n); n.Width = 80; using var second = Render(r, n);
            Check(r.EffectBuilds == 2 && second.GetPixel(77, 50).Red > 240 && second.GetPixel(77, 50).Green < 10);
        });
        test("effect cache invalidates only on effect mutations", () =>
        { using var r = new SceneRenderer(); var n = Node(new FillStyle()); n.Shadows.Add(new()); using var b = Render(r, n); n.X = 5; using var c = Render(r, n); Check(r.EffectBuilds == 1); n.Shadows[0].Spread = 3; using var d = Render(r, n); Check(r.EffectBuilds == 2); });
        test("node opacity applies once to content and shadow composite", () =>
        { using var r = new SceneRenderer(); var n = Node(new FillStyle()); n.Opacity = .5; n.Shadows.Add(new() { X = 10, Blur = 0, Opacity = 1 }); using var b = Render(r, n); Near(b.GetPixel(50, 50).Alpha, 127, 1); });
        test("appearance validation rejects invalid geometry and excessive effects", () =>
        {
            var n = Node(Gradient()); n.Fills[0].Stops[0].Opacity = double.NaN; Reject(() => DocumentJson.Validate(Document(n)));
            n.Fills[0].Stops[0].Opacity = 1; n.Fills[0].GradientTransform = Matrix2D.Scale(0, 1); Reject(() => DocumentJson.Validate(Document(n)));
            n.Fills.Clear(); n.Shadows = Enumerable.Range(0, 33).Select(_ => new ShadowStyle()).ToList(); Reject(() => DocumentJson.Validate(Document(n)));
        });
        test("appearance v4 roundtrip retains image crop filters and effect stack", () =>
        {
            var n = Node(Image(ImageScaleMode.Crop)); n.Fills[0].ImageOffset = new(.25, -.3); n.Fills[0].Exposure = .5;
            n.Shadows = [new() { Kind = EffectKind.InnerShadow, Spread = 8 }, new() { Kind = EffectKind.LayerBlur }];
            var read = DocumentJson.Load(DocumentJson.Save(Document(n))); Check(read.FormatVersion == DesignDocument.CurrentFormatVersion); var c = read.Pages[0].Nodes[0]; Check(c.Fills[0].ImageData == n.Fills[0].ImageData && c.Fills[0].ImageOffset == n.Fills[0].ImageOffset); Check(c.Shadows[0].Kind == EffectKind.InnerShadow);
        });
        foreach (var version in new[] { 1, 2, 3 }) test("appearance migration preserves legacy shadow v" + version, () =>
        { var d = Document(Node(new FillStyle())); d.FormatVersion = version; d.Pages[0].Nodes[0].Shadows.Add(new()); var read = DocumentJson.Load(DocumentJson.Save(d)); Check(read.FormatVersion == DesignDocument.CurrentFormatVersion && read.Pages[0].Nodes[0].Shadows[0].Kind == EffectKind.DropShadow); });
        test("image clipboard is self-contained across documents and undoable", () =>
        {
            var source = new EditorSession(Document(Node(Image()))); source.Select(source.Page.Nodes[0]); var target = new EditorSession(new()); target.Paste(source.CopySelection());
            Check(target.Primary!.Fills[0].ImageData == source.Primary!.Fills[0].ImageData); target.Undo(); Check(target.Page.Nodes.Count == 0); target.Redo(); Check(target.Primary!.Fills[0].Kind == FillKind.Image);
        });
        test("image and effect overrides survive synchronization and reset", () =>
        {
            var c = Node(new FillStyle() { Color = "#FF0000" }); c.Kind = NodeKind.Component; var e = new EditorSession(Document(c)); var instance = ComponentService.InsertInstance(e, c, new(200, 0));
            e.Edit("Image override", () => { instance.Fills = [Image()]; instance.Shadows = [new() { Kind = EffectKind.InnerShadow }]; ComponentService.SetAppearanceOverride(instance); });
            e.Edit("Source edit", () => c.CornerRadius = 8); Check(instance.Fills[0].Kind == FillKind.Image && instance.Shadows.Count == 1); Check(!ReferenceEquals(instance.Fills, instance.Overrides[c.Id].Fills));
            e.Select(instance); ComponentService.ResetOverrides(e); Check(instance.Fills[0].Kind == FillKind.Solid && instance.Shadows.Count == 0);
        });
        test("SVG imports inherited gradients instead of purple placeholders", () =>
        {
            var svg = "<svg width='100' height='100'><defs><linearGradient id='base'><stop offset='0' stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/></linearGradient><linearGradient id='g' href='#base' x2='0' y2='1'/></defs><rect width='100' height='100' fill='url(#g)'/></svg>";
            var read = SvgFormat.Import(svg); Check(read.Warnings.Count == 0); var node = read.Document.Pages[0].Nodes[0].Children[0]; Check(node.Fills[0].Kind == FillKind.LinearGradient && node.Fills[0].Stops.Count == 2); Near(node.Fills[0].End.Y, 1);
            using var r = new SceneRenderer(); using var b = Render(r, node); Check(b.GetPixel(50, 10).Red > 220 && b.GetPixel(50, 90).Blue > 220);
        });
        test("SVG imports stop opacity and CSS alpha ordering", () =>
        {
            var d = SvgFormat.Import("<svg><defs><linearGradient id='g'><stop stop-color='#ff000080'/><stop offset='1' style='stop-color:#ff0000;stop-opacity:0.5'/></linearGradient></defs><rect width='100' height='100' fill='url(#g)' fill-opacity='0.5'/></svg>");
            using var r = new SceneRenderer(); using var b = Render(r, d.Document.Pages[0].Nodes[0].Children[0]); Near(b.GetPixel(20, 20).Alpha, 64, 1); Check(b.GetPixel(20, 20).Red > 240);
        });
        test("SVG user-space gradients account for shape translation", () =>
        {
            var d = SvgFormat.Import("<svg width='200' height='100'><defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x1='50' x2='150'><stop stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/></linearGradient></defs><rect x='50' width='100' height='100' fill='url(#g)'/></svg>");
            using var r = new SceneRenderer(); using var b = Render(r, d.Document.Pages[0].Nodes[0], new(0, 0, 200, 100)); Check(b.GetPixel(55, 50).Red > 230 && b.GetPixel(145, 50).Blue > 230);
        });
        test("SVG viewBox scales nested groups and user-space gradient percentages together", () =>
        {
            var d = SvgFormat.Import("<svg width='200' height='100' viewBox='0 0 100 50'><defs><linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0%' x2='100%'><stop stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/></linearGradient></defs><g><rect width='100' height='50' fill='url(#g)'/></g></svg>");
            var f = d.Document.Pages[0].Nodes[0]; var n = f.Children[0].Children[0]; Near(n.Width, 200); Near(n.Height, 100);
            using var r = new SceneRenderer(); using var b = Render(r, f, new(0, 0, 200, 100)); Check(b.GetPixel(5, 50).Red > 230 && b.GetPixel(195, 50).Blue > 230);
        });
        test("SVG viewBox meet defaults to centered letterboxing", () =>
        {
            var d = SvgFormat.Import("<svg width='200' height='100' viewBox='10 20 100 100'><rect x='10' y='20' width='100' height='100' fill='#ff0000'/></svg>");
            var n = d.Document.Pages[0].Nodes[0].Children[0]; Near(n.X, 50); Near(n.Y, 0); Near(n.Width, 100); Near(n.Height, 100);
        });
        test("SVG nested transformed groups bake scale through descendants", () =>
        {
            var d = SvgFormat.Import("<svg width='200' height='200'><g transform='translate(10,20) scale(2)'><g><rect x='5' y='6' width='30' height='20' fill='#ff0000'/></g></g></svg>");
            var n = d.Document.Pages[0].Nodes[0].Children[0].Children[0].Children[0]; Near(n.WorldBounds.X, 20); Near(n.WorldBounds.Y, 32); Near(n.Width, 60); Near(n.Height, 40);
        });
        test("SVG radial geometry and repeat spread survive export", () =>
        {
            var f = Gradient(); f.Kind = FillKind.RadialGradient; f.Start = new(.2, .3); f.GradientRadius = .4; f.GradientFocal = new(.1, .2); f.Spread = GradientSpread.Reflect;
            var xml = XDocument.Parse(SvgFormat.Export([Node(f)], new(0, 0, 100, 100))); var g = xml.Descendants().Single(e => e.Name.LocalName == "radialGradient");
            Check(g.Attribute("cx")!.Value == "0.2" && g.Attribute("fy")!.Value == "0.2" && g.Attribute("spreadMethod")!.Value == "reflect");
        });
        test("SVG gradient cycles terminate and report a repairable omission", () =>
        { var d = SvgFormat.Import("<svg><defs><linearGradient id='a' href='#b'/><linearGradient id='b' href='#a'/></defs><rect width='100' height='100' fill='url(#a)'/></svg>"); Check(d.Warnings.Any(w => w.Contains("Cyclic"))); Check(d.Document.Pages[0].Nodes[0].Children[0].Fills.Count == 0); });
        test("SVG inline style overrides presentation attributes", () =>
        { var d = SvgFormat.Import("<svg><rect width='100' height='100' fill='#ff0000' style='fill:#0000ff;opacity:0.5'/></svg>"); var n = d.Document.Pages[0].Nodes[0].Children[0]; Check(n.Fill == "#0000ff"); Near(n.Opacity, .5); });
        test("SVG embedded image import remains editable", () =>
        { var f = Image(); var d = SvgFormat.Import($"<svg width='100' height='100'><image width='100' height='100' href='{f.ImageData}' preserveAspectRatio='xMidYMid slice'/></svg>"); var n = d.Document.Pages[0].Nodes[0].Children[0]; Check(n.Fills[0].Kind == FillKind.Image && n.Fills[0].ImageMode == ImageScaleMode.Fill); });
        test("SVG image export retains data placement and adjustment filters", () =>
        { var f = Image(ImageScaleMode.Crop); f.Contrast = .3; var xml = XDocument.Parse(SvgFormat.Export([Node(f)], new(0, 0, 100, 100))); Check(xml.Descendants().Any(e => e.Name.LocalName == "image" && e.Attribute("href")?.Value == f.ImageData)); Check(xml.Descendants().Any(e => e.Name.LocalName == "feColorMatrix")); });
        test("SVG effect graph exports all shadows and independent source alpha", () =>
        { var n = Node(new FillStyle()); n.Shadows = [new(), new() { Kind = EffectKind.InnerShadow }, new() { Kind = EffectKind.LayerBlur }]; var xml = XDocument.Parse(SvgFormat.Export([n], n.LocalBounds)); Check(xml.Descendants().Count(e => e.Name.LocalName == "feGaussianBlur") == 3); Check(xml.Descendants().Count(e => e.Name.LocalName == "feFlood") == 2); });
    }
}
