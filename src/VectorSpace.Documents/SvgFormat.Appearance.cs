using System.Globalization;
using System.Xml.Linq;
using VectorSpace.Core;

namespace VectorSpace.Documents;

public static partial class SvgFormat
{
    private static (string Color, double Alpha) CssColor(string value)
    {
        if (value.Length == 9 && value[0] == '#' && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var native))
            return ("#" + (native & 0xffffff).ToString("X6"), (native >> 24) / 255d);
        return (value, value.Equals("transparent", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
    }
    private static string ImportCssColor(string value)
    {
        value = value.Trim();
        if (value.StartsWith('#'))
        {
            if (value.Length == 9) return "#" + value[7..9] + value[1..7];
            if (value.Length == 5) return "#" + new string(value[4], 2) + new string(value[1], 2) + new string(value[2], 2) + new string(value[3], 2);
            if (value.Length == 4) return "#" + new string(value[1], 2) + new string(value[2], 2) + new string(value[3], 2);
        }
        return value;
    }
    private static XElement ExportGradient(FillStyle fill, DesignNode node, string id)
    {
        var gradient = new XElement(Ns + (fill.Kind == FillKind.LinearGradient ? "linearGradient" : "radialGradient"), new XAttribute("id", id),
            new XAttribute("gradientUnits", fill.GradientUserSpace ? "userSpaceOnUse" : "objectBoundingBox"),
            new XAttribute("gradientTransform", Transform(fill.GradientTransform)), new XAttribute("spreadMethod", fill.Spread switch { GradientSpread.Repeat => "repeat", GradientSpread.Reflect => "reflect", _ => "pad" }));
        if (fill.Kind == FillKind.LinearGradient)
        {
            gradient.SetAttributeValue("x1", F(fill.Start.X)); gradient.SetAttributeValue("y1", F(fill.Start.Y));
            gradient.SetAttributeValue("x2", F(fill.End.X)); gradient.SetAttributeValue("y2", F(fill.End.Y));
        }
        else
        {
            var center = fill.Start; var focal = fill.GradientFocal ?? center; var radius = fill.GradientRadius;
            if (radius is null)
            {
                center = new(center.X * node.Width, center.Y * node.Height); focal = center;
                radius = center.DistanceTo(new(fill.End.X * node.Width, fill.End.Y * node.Height));
                gradient.SetAttributeValue("gradientUnits", "userSpaceOnUse");
            }
            gradient.SetAttributeValue("cx", F(center.X)); gradient.SetAttributeValue("cy", F(center.Y)); gradient.SetAttributeValue("r", F(Math.Max(.000001, radius.Value)));
            gradient.SetAttributeValue("fx", F(focal.X)); gradient.SetAttributeValue("fy", F(focal.Y));
        }
        foreach (var stop in fill.Stops.OrderBy(s => s.Offset))
        {
            var color = CssColor(stop.Color);
            gradient.Add(new XElement(Ns + "stop", new XAttribute("offset", F(stop.Offset)), new XAttribute("stop-color", color.Color), new XAttribute("stop-opacity", F(color.Alpha * stop.Opacity))));
        }
        return gradient;
    }
    private static XElement? ExportImage(FillStyle fill, DesignNode node, XElement defs, string id)
    {
        if (fill.ImageData is null) return null;
        EmbeddedImage.Validate(fill.ImageData);
        var size = ImageHeader.Read(EmbeddedImage.Decode(fill.ImageData).Bytes);
        var mapping = ImagePlacement.Calculate(fill, node.Width, node.Height, size.Width, size.Height);
        var image = new XElement(Ns + "image", new XAttribute("width", size.Width), new XAttribute("height", size.Height), new XAttribute("href", fill.ImageData));
        var clipId = id + "-clip"; defs.Add(new XElement(Ns + "clipPath", new XAttribute("id", clipId), Shape(node)));
        var group = new XElement(Ns + "g", new XAttribute("clip-path", "url(#" + clipId + ")"), new XAttribute("opacity", F(fill.Opacity)));
        if (fill.Blend != BlendKind.Normal) group.SetAttributeValue("style", "mix-blend-mode:" + fill.Blend.ToString().ToLowerInvariant());
        if (fill.ImageMode == ImageScaleMode.Tile)
        {
            var patternId = id + "-tile";
            defs.Add(new XElement(Ns + "pattern", new XAttribute("id", patternId), new XAttribute("patternUnits", "userSpaceOnUse"), new XAttribute("width", size.Width), new XAttribute("height", size.Height), new XAttribute("patternTransform", Transform(mapping)), image));
            var shape = Shape(node); shape.SetAttributeValue("fill", "url(#" + patternId + ")"); group.Add(shape);
        }
        else { image.SetAttributeValue("transform", Transform(mapping)); group.Add(image); }
        if (fill.Exposure != 0 || fill.Contrast != 0 || fill.Saturation != 0)
        {
            var fid = id + "-adjust"; var sat = 1 + fill.Saturation; var k = (1 + fill.Contrast) * Math.Pow(2, fill.Exposure);
            var r = (1 - sat) * .2126; var g = (1 - sat) * .7152; var b = (1 - sat) * .0722; var t = -.5 * fill.Contrast;
            var values = new[] { k * (r + sat), k * g, k * b, 0, t, k * r, k * (g + sat), k * b, 0, t, k * r, k * g, k * (b + sat), 0, t, 0, 0, 0, 1, 0 };
            defs.Add(new XElement(Ns + "filter", new XAttribute("id", fid), new XAttribute("color-interpolation-filters", "sRGB"), new XElement(Ns + "feColorMatrix", new XAttribute("type", "matrix"), new XAttribute("values", string.Join(" ", values.Select(F))))));
            group.SetAttributeValue("filter", "url(#" + fid + ")");
        }
        return group;
    }
    private static XElement ExportEffects(DesignNode node, string id)
    {
        var padding = node.Shadows.Where(s => s.Visible).Sum(s => Math.Abs(s.X) + Math.Abs(s.Y) + Math.Abs(s.Spread) + s.Blur * 3) + 1;
        var filter = new XElement(Ns + "filter", new XAttribute("id", id), new XAttribute("filterUnits", "userSpaceOnUse"), new XAttribute("x", F(-padding)), new XAttribute("y", F(-padding)),
            new XAttribute("width", F(node.Width + padding * 2)), new XAttribute("height", F(node.Height + padding * 2)), new XAttribute("color-interpolation-filters", "sRGB"));
        var drop = new List<string>(); var inner = new List<string>(); var i = 0;
        foreach (var s in node.Shadows.Where(s => s.Visible && s.Kind != EffectKind.LayerBlur).Reverse())
        {
            var prefix = "s" + i++; var input = "SourceAlpha";
            if (s.Spread != 0)
            {
                var expand = s.Kind == EffectKind.DropShadow ? s.Spread > 0 : s.Spread < 0;
                filter.Add(new XElement(Ns + "feMorphology", new XAttribute("in", input), new XAttribute("operator", expand ? "dilate" : "erode"), new XAttribute("radius", F(Math.Abs(s.Spread))), new XAttribute("result", prefix + "spread"))); input = prefix + "spread";
            }
            filter.Add(new XElement(Ns + "feOffset", new XAttribute("in", input), new XAttribute("dx", F(s.X)), new XAttribute("dy", F(s.Y)), new XAttribute("result", prefix + "offset")));
            filter.Add(new XElement(Ns + "feGaussianBlur", new XAttribute("in", prefix + "offset"), new XAttribute("stdDeviation", F(s.Blur / 2)), new XAttribute("result", prefix + "blur")));
            var color = CssColor(s.Color);
            filter.Add(new XElement(Ns + "feFlood", new XAttribute("flood-color", color.Color), new XAttribute("flood-opacity", F(s.Opacity * color.Alpha)), new XAttribute("result", prefix + "color")));
            if (s.Kind == EffectKind.InnerShadow)
            {
                filter.Add(new XElement(Ns + "feComposite", new XAttribute("in", prefix + "color"), new XAttribute("in2", prefix + "blur"), new XAttribute("operator", "out"), new XAttribute("result", prefix))); inner.Add(prefix);
            }
            else
            {
                filter.Add(new XElement(Ns + "feComposite", new XAttribute("in", prefix + "color"), new XAttribute("in2", prefix + "blur"), new XAttribute("operator", "in"), new XAttribute("result", prefix))); drop.Add(prefix);
            }
        }
        var content = "SourceGraphic";
        foreach (var shadow in inner)
        {
            var output = shadow + "atop";
            filter.Add(new XElement(Ns + "feComposite", new XAttribute("in", shadow), new XAttribute("in2", content), new XAttribute("operator", "atop"), new XAttribute("result", output)));
            content = output;
        }
        var merge = new XElement(Ns + "feMerge", new XAttribute("result", "composite"));
        foreach (var input in drop.Append(content)) merge.Add(new XElement(Ns + "feMergeNode", new XAttribute("in", input))); filter.Add(merge);
        var last = "composite";
        foreach (var blur in node.Shadows.Where(s => s.Visible && s.Kind == EffectKind.LayerBlur))
        {
            var output = "b" + i++; filter.Add(new XElement(Ns + "feGaussianBlur", new XAttribute("in", last), new XAttribute("stdDeviation", F(blur.Blur / 2)), new XAttribute("result", output))); last = output;
        }
        return filter;
    }
    private static Matrix2D SvgViewBoxTransform(double[] box, double width, double height, string? preserve)
    {
        var sx = width / box[2]; var sy = height / box[3];
        var options = (preserve ?? "xMidYMid meet").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var align = options.FirstOrDefault() ?? "xMidYMid"; double dx = 0, dy = 0;
        if (align != "none")
        {
            sx = sy = options.Contains("slice") ? Math.Max(sx, sy) : Math.Min(sx, sy);
            dx = (width - box[2] * sx) * (align.Contains("xMax", StringComparison.Ordinal) ? 1 : align.Contains("xMin", StringComparison.Ordinal) ? 0 : .5);
            dy = (height - box[3] * sy) * (align.Contains("YMax", StringComparison.Ordinal) ? 1 : align.Contains("YMin", StringComparison.Ordinal) ? 0 : .5);
        }
        return Matrix2D.Translation(-box[0], -box[1]) * Matrix2D.Scale(sx, sy) * Matrix2D.Translation(dx, dy);
    }
    private static void ApplySvgTransform(DesignNode node, Matrix2D matrix, HashSet<string> warnings)
    {
        var sx = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var determinant = matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21;
        var sy = sx > 1e-9 ? Math.Abs(determinant / sx) : 1;
        if (Math.Abs(matrix.M11 * matrix.M21 + matrix.M12 * matrix.M22) > 1e-7)
            warnings.Add("Affine skew is approximated by the native rigid/size transform model.");
        NodeGeometry.SetLocalMatrix(node, matrix);
        if (Math.Abs(sx - 1) < 1e-9 && Math.Abs(sy - 1) < 1e-9) return;
        var scale = Matrix2D.Scale(Math.Max(.0001, sx), Math.Max(.0001, sy));
        foreach (var fill in node.Fills)
            if (fill.GradientUserSpace) fill.GradientTransform *= scale;
        foreach (var stroke in node.Strokes)
        {
            stroke.Width *= (sx + sy) / 2;
            for (var i = 0; i < stroke.Dashes.Count; i++) stroke.Dashes[i] *= (sx + sy) / 2;
        }
        node.CornerRadius *= Math.Min(sx, sy);
        if (node.Kind == NodeKind.Text)
        {
            node.FontSize *= sy; node.LetterSpacing *= sx;
            if (Math.Abs(sx - sy) > 1e-7) warnings.Add("Nonuniform text scaling is approximated by font size and spacing.");
        }
        // Native containers express scale through size rather than their matrix. Bake that
        // scale through descendants so transformed SVG groups do not leave children behind.
        foreach (var child in node.Children) ApplySvgTransform(child, child.LocalMatrix * scale, warnings);
    }
    private static FillStyle? ReadGradient(string reference, Dictionary<string, XElement> definitions, DesignNode node, double viewportWidth, double viewportHeight, HashSet<string> warnings)
    {
        var match = PaintReferenceRegex().Match(reference);
        if (!match.Success || !definitions.TryGetValue(match.Groups[1].Value, out var element)) { warnings.Add("Unresolved or external paint server was omitted."); return null; }
        var chain = new List<XElement>(); var visited = new HashSet<XElement>(ReferenceEqualityComparer.Instance);
        while (true)
        {
            if (!visited.Add(element) || chain.Count >= 32) { warnings.Add("Cyclic or over-deep gradient inheritance was omitted."); return null; }
            if (element.Name.LocalName is not ("linearGradient" or "radialGradient")) { warnings.Add("This SVG paint server type is not supported."); return null; }
            chain.Add(element);
            var href = element.Attribute("href")?.Value ?? element.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Value;
            if (href is null) break;
            if (!href.StartsWith('#') || !definitions.TryGetValue(href[1..], out element!)) { warnings.Add("Unresolved gradient inheritance was omitted."); return null; }
        }
        string? Attribute(string name) => chain.Select(e => e.Attribute(name)?.Value).FirstOrDefault(v => v is not null);
        var userSpace = Attribute("gradientUnits") == "userSpaceOnUse";
        double Coordinate(string name, string fallback, double axis)
        {
            var value = Attribute(name) ?? fallback; var percent = value.EndsWith('%');
            var n = Numbers.Parse(percent ? value[..^1] : value);
            return percent ? n / 100 * (userSpace ? axis : 1) : n;
        }
        var fill = new FillStyle { Kind = chain[0].Name.LocalName == "linearGradient" ? FillKind.LinearGradient : FillKind.RadialGradient,
            GradientUserSpace = userSpace, GradientTransform = ParseTransform(Attribute("gradientTransform") ?? ""), Stops = [],
            Spread = Attribute("spreadMethod") switch { "repeat" => GradientSpread.Repeat, "reflect" => GradientSpread.Reflect, _ => GradientSpread.Pad } };
        if (userSpace) fill.GradientTransform *= Matrix2D.Translation(-node.X, -node.Y);
        if (fill.Kind == FillKind.LinearGradient)
        { fill.Start = new(Coordinate("x1", "0%", viewportWidth), Coordinate("y1", "0%", viewportHeight)); fill.End = new(Coordinate("x2", "100%", viewportWidth), Coordinate("y2", "0%", viewportHeight)); }
        else
        {
            fill.Start = new(Coordinate("cx", "50%", viewportWidth), Coordinate("cy", "50%", viewportHeight));
            fill.GradientRadius = Math.Max(.000001, Coordinate("r", "50%", Math.Sqrt((viewportWidth * viewportWidth + viewportHeight * viewportHeight) / 2)));
            fill.GradientFocal = new(Coordinate("fx", Attribute("cx") ?? "50%", viewportWidth), Coordinate("fy", Attribute("cy") ?? "50%", viewportHeight));
        }
        var stopSource = chain.FirstOrDefault(e => e.Elements().Any(c => c.Name.LocalName == "stop"));
        double lastOffset = 0;
        if (stopSource is not null) foreach (var stop in stopSource.Elements().Where(e => e.Name.LocalName == "stop"))
        {
            var offset = stop.Attribute("offset")?.Value ?? "0";
            var value = Numbers.Parse(offset.TrimEnd('%')) / (offset.EndsWith('%') ? 100 : 1); lastOffset = Math.Clamp(value, lastOffset, 1);
            fill.Stops.Add(new() { Offset = lastOffset, Color = ImportCssColor(Style(stop, "stop-color") ?? stop.Attribute("stop-color")?.Value ?? "#000000"),
                Opacity = Math.Clamp(Numbers.Parse(Style(stop, "stop-opacity") ?? stop.Attribute("stop-opacity")?.Value ?? "1", 1), 0, 1) });
        }
        if (fill.Stops.Count == 0) fill.Stops.Add(new() { Color = "#00000000" });
        if (node.Kind == NodeKind.Path && !userSpace) warnings.Add("Object-bounding-box gradients on imported paths use the imported path frame; exact path-bounds normalization is not yet supported.");
        return fill;
    }
    [System.Text.RegularExpressions.GeneratedRegex("^url\\(\\s*['\"]?#([^)'\"\\s]+)['\"]?\\s*\\)$")]
    private static partial System.Text.RegularExpressions.Regex PaintReferenceRegex();
}
