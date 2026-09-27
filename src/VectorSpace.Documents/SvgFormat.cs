using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using VectorSpace.Core;

namespace VectorSpace.Documents;

public sealed record SvgImportResult(DesignDocument Document, IReadOnlyList<string> Warnings);

/// <summary>Safe, editable SVG interchange. Scripts, external resources and DTDs are never executed.</summary>
public static partial class SvgFormat
{
    private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
    public static string Export(IEnumerable<DesignNode> roots, RectD bounds)
    {
        var defs = new XElement(Ns + "defs");
        var svg = new XElement(Ns + "svg", new XAttribute("width", F(bounds.Width)), new XAttribute("height", F(bounds.Height)), new XAttribute("viewBox", $"{F(bounds.X)} {F(bounds.Y)} {F(bounds.Width)} {F(bounds.Height)}"), defs);
        foreach (var node in roots) svg.Add(ExportNode(node, defs, true));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), svg).ToString();
    }
    private static XElement? ExportNode(DesignNode node, XElement defs, bool world = false)
    {
        if (!node.Visible || node.Kind == NodeKind.Slice) return null;
        var group = new XElement(Ns + "g", new XAttribute("id", "layer-" + node.Id), new XAttribute("data-name", node.Name), new XAttribute("transform", Transform(world ? node.WorldMatrix : node.LocalMatrix)), new XAttribute("opacity", F(node.Opacity)));
        if (node.Blend != BlendKind.Normal) group.SetAttributeValue("style", "mix-blend-mode:" + node.Blend.ToString().ToLowerInvariant());
        var shadow = node.Shadows.FirstOrDefault(s => s.Visible);
        if (shadow is not null)
        {
            var filterId = "shadow-" + node.Id;
            defs.Add(new XElement(Ns + "filter", new XAttribute("id", filterId), new XAttribute("x", "-100%"), new XAttribute("y", "-100%"), new XAttribute("width", "300%"), new XAttribute("height", "300%"), new XElement(Ns + "feDropShadow", new XAttribute("dx", F(shadow.X)), new XAttribute("dy", F(shadow.Y)), new XAttribute("stdDeviation", F(shadow.Blur / 2)), new XAttribute("flood-color", shadow.Color), new XAttribute("flood-opacity", F(shadow.Opacity)))));
            group.SetAttributeValue("filter", "url(#" + filterId + ")");
        }
        for (var i = 0; i < node.Fills.Count; i++)
        {
            var fill = node.Fills[i]; if (!fill.Visible) continue; var color = fill.Color;
            if (fill.Kind != FillKind.Solid)
            {
                var id = $"paint-{node.Id}-{i}";
                var gradient = new XElement(Ns + (fill.Kind == FillKind.LinearGradient ? "linearGradient" : "radialGradient"), new XAttribute("id", id));
                if (fill.Kind == FillKind.LinearGradient) { gradient.SetAttributeValue("x1", F(fill.Start.X)); gradient.SetAttributeValue("y1", F(fill.Start.Y)); gradient.SetAttributeValue("x2", F(fill.End.X)); gradient.SetAttributeValue("y2", F(fill.End.Y)); }
                foreach (var stop in fill.Stops.OrderBy(s => s.Offset)) gradient.Add(new XElement(Ns + "stop", new XAttribute("offset", F(stop.Offset)), new XAttribute("stop-color", stop.Color)));
                defs.Add(gradient); color = "url(#" + id + ")";
            }
            var shape = Shape(node); shape.SetAttributeValue("fill", color); shape.SetAttributeValue("fill-opacity", F(fill.Opacity)); shape.SetAttributeValue("stroke", "none"); group.Add(shape);
        }
        foreach (var stroke in node.Strokes.Where(s => s.Visible))
        {
            var shape = Shape(node); shape.SetAttributeValue("fill", "none"); shape.SetAttributeValue("stroke", stroke.Color); shape.SetAttributeValue("stroke-width", F(stroke.Width)); shape.SetAttributeValue("stroke-opacity", F(stroke.Opacity)); shape.SetAttributeValue("stroke-linejoin", "round"); shape.SetAttributeValue("stroke-linecap", "round");
            if (stroke.Dashes.Count > 0) shape.SetAttributeValue("stroke-dasharray", string.Join(" ", stroke.Dashes.Select(F))); group.Add(shape);
        }
        var children = new XElement(Ns + "g");
        if (node.ClipContent)
        {
            var id = "clip-" + node.Id; defs.Add(new XElement(Ns + "clipPath", new XAttribute("id", id), new XElement(Ns + "rect", new XAttribute("width", F(node.Width)), new XAttribute("height", F(node.Height)), new XAttribute("rx", F(node.CornerRadius))))); children.SetAttributeValue("clip-path", "url(#" + id + ")");
        }
        foreach (var child in node.Children) children.Add(ExportNode(child, defs));
        if (children.HasElements) group.Add(children); return group;
    }
    private static XElement Shape(DesignNode node)
    {
        if (node.Kind == NodeKind.Text)
        {
            var anchor = node.TextAlign == TextAlignment.Center ? "middle" : node.TextAlign == TextAlignment.Right ? "end" : "start";
            var x = node.TextAlign == TextAlignment.Center ? node.Width / 2 : node.TextAlign == TextAlignment.Right ? node.Width : 0;
            var element = new XElement(Ns + "text", new XAttribute("font-family", node.FontFamily), new XAttribute("font-size", F(node.FontSize)), new XAttribute("font-weight", node.FontWeight), new XAttribute("letter-spacing", F(node.LetterSpacing)), new XAttribute("text-anchor", anchor));
            var lines = node.Text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++) element.Add(new XElement(Ns + "tspan", new XAttribute("x", F(x)), new XAttribute("y", F(node.FontSize + i * node.FontSize * node.LineHeight)), lines[i]));
            return element;
        }
        var shape = new XElement(Ns + "path", new XAttribute("d", VectorPath.Build(node)));
        if (node.Kind == NodeKind.Path && node.PathWidth > 0 && node.PathHeight > 0) shape.SetAttributeValue("transform", $"scale({F(node.Width / node.PathWidth)} {F(node.Height / node.PathHeight)})");
        return shape;
    }
    public static SvgImportResult Import(string source, string name = "Imported SVG")
    {
        if (source.Length > DocumentJson.MaxDocumentCharacters) throw new InvalidDataException("SVG exceeds the import limit.");
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = DocumentJson.MaxDocumentCharacters };
        using var text = new StringReader(source); using var reader = XmlReader.Create(text, settings);
        var xml = XDocument.Load(reader); var root = xml.Root ?? throw new InvalidDataException("SVG is empty.");
        if (root.Name.LocalName != "svg") throw new InvalidDataException("The file root must be svg.");
        var warnings = new HashSet<string>(); var viewBox = Values(root.Attribute("viewBox")?.Value);
        var width = Number(root, "width", viewBox.Length == 4 ? viewBox[2] : 800); var height = Number(root, "height", viewBox.Length == 4 ? viewBox[3] : 600);
        var frame = new DesignNode { Kind = NodeKind.Frame, Name = name, Width = Math.Max(1, width), Height = Math.Max(1, height), Fills = [], ClipContent = true };
        var count = 0;
        foreach (var child in root.Elements())
        {
            var node = Read(child, 0); if (node is not null) frame.Add(node);
        }
        if (viewBox.Length == 4 && viewBox[2] > 0 && viewBox[3] > 0)
        {
            var transform = Matrix2D.Translation(-viewBox[0], -viewBox[1]) * Matrix2D.Scale(width / viewBox[2], height / viewBox[3]);
            foreach (var n in frame.Children) NodeGeometry.SetLocalMatrix(n, n.LocalMatrix * transform);
        }
        var document = new DesignDocument { Name = name, Pages = [new() { Nodes = [frame] }] }; document.RebuildParents(); DocumentJson.Validate(document); return new(document, warnings.ToArray());
        DesignNode? Read(XElement element, int depth)
        {
            if (++count > DocumentJson.MaxNodes || depth > 60) throw new InvalidDataException("SVG node or nesting limit exceeded.");
            var kind = element.Name.LocalName;
            if (kind is "defs" or "title" or "desc" or "metadata") return null;
            if (kind is "script" or "foreignObject" or "image" or "use" or "style" or "filter" or "clipPath" or "mask") { warnings.Add($"{kind} elements were not imported."); return null; }
            var node = new DesignNode { Name = element.Attribute("data-name")?.Value ?? element.Attribute("id")?.Value ?? kind, X = Number(element, "x"), Y = Number(element, "y"), Width = Number(element, "width", width), Height = Number(element, "height", height), Fills = [] };
            switch (kind)
            {
                case "g": case "svg": node.Kind = NodeKind.Group; node.X = node.Y = 0; break;
                case "rect": node.Kind = NodeKind.Rectangle; node.CornerRadius = Number(element, "rx"); break;
                case "circle": var radius = Number(element, "r"); node.Kind = NodeKind.Ellipse; node.Width = node.Height = radius * 2; node.X = Number(element, "cx") - radius; node.Y = Number(element, "cy") - radius; break;
                case "ellipse": node.Kind = NodeKind.Ellipse; node.Width = Number(element, "rx") * 2; node.Height = Number(element, "ry") * 2; node.X = Number(element, "cx") - node.Width / 2; node.Y = Number(element, "cy") - node.Height / 2; break;
                case "line": node.Kind = NodeKind.Path; node.Points = [new() { Position = new(Number(element, "x1"), Number(element, "y1")) }, new() { Position = new(Number(element, "x2"), Number(element, "y2")) }]; node.PathWidth = node.Width; node.PathHeight = node.Height; break;
                case "polygon": case "polyline": var numbers = Values(element.Attribute("points")?.Value); node.Kind = NodeKind.Path; node.Closed = kind == "polygon"; node.PathWidth = node.Width; node.PathHeight = node.Height; for (var i = 0; i + 1 < numbers.Length; i += 2) node.Points.Add(new() { Position = new(numbers[i], numbers[i + 1]) }); break;
                case "path": node.Kind = NodeKind.Path; node.PathData = element.Attribute("d")?.Value ?? ""; node.PathWidth = node.Width; node.PathHeight = node.Height; break;
                case "text": node.Kind = NodeKind.Text; node.Text = element.Value; node.FontSize = Number(element, "font-size", 16); node.FontFamily = element.Attribute("font-family")?.Value ?? "Inter"; node.FontWeight = (int)Number(element, "font-weight", 400); node.Y -= node.FontSize; node.Width = Math.Max(1, node.Text.Length * node.FontSize * .6); node.Height = node.FontSize * 1.3; break;
                default: warnings.Add($"{kind} elements were not imported."); return null;
            }
            string? Attribute(string key) => element.Attribute(key)?.Value ?? Style(element, key) ?? element.Ancestors().Select(a => a.Attribute(key)?.Value ?? Style(a, key)).FirstOrDefault(v => v is not null);
            var fill = Attribute("fill") ?? "#000000";
            if (kind is not "g" and not "svg" && fill != "none")
            {
                if (fill.StartsWith("url", StringComparison.OrdinalIgnoreCase)) { warnings.Add("Referenced paint servers currently import as a solid fill."); fill = "#A78BFA"; }
                node.Fills.Add(new() { Color = fill, Opacity = Numbers.Parse(Attribute("fill-opacity") ?? "1", 1) });
            }
            var stroke = Attribute("stroke"); if (stroke is not null && stroke != "none") node.Strokes.Add(new() { Color = stroke, Width = Numbers.Parse(Attribute("stroke-width") ?? "1", 1), Opacity = Numbers.Parse(Attribute("stroke-opacity") ?? "1", 1) });
            node.Opacity = Number(element, "opacity", 1); node.Visible = Attribute("display") != "none" && Attribute("visibility") != "hidden";
            if (element.Attribute("transform") is { } attribute) NodeGeometry.SetLocalMatrix(node, node.LocalMatrix * ParseTransform(attribute.Value));
            if (kind is "g" or "svg") foreach (var child in element.Elements()) { var c = Read(child, depth + 1); if (c is not null) node.Add(c); }
            return node;
        }
    }
    private static string? Style(XElement element, string key) => element.Attribute("style")?.Value.Split(';').Select(x => x.Split(':', 2)).FirstOrDefault(x => x.Length == 2 && x[0].Trim() == key)?[1].Trim();
    private static double Number(XElement element, string name, double fallback = 0) => Values(element.Attribute(name)?.Value).FirstOrDefault(fallback);
    private static double[] Values(string? text) => text is null ? [] : NumberRegex().Matches(text).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
    public static Matrix2D ParseTransform(string text)
    {
        var result = Matrix2D.Identity;
        foreach (Match match in TransformRegex().Matches(text))
        {
            var values = Values(match.Groups[2].Value); var matrix = Matrix2D.Identity;
            switch (match.Groups[1].Value)
            {
                case "matrix" when values.Length == 6: matrix = new(values[0], values[1], values[2], values[3], values[4], values[5]); break;
                case "translate" when values.Length >= 1: matrix = Matrix2D.Translation(values[0], values.Length > 1 ? values[1] : 0); break;
                case "scale" when values.Length >= 1: matrix = Matrix2D.Scale(values[0], values.Length > 1 ? values[1] : values[0]); break;
                case "rotate" when values.Length >= 1: matrix = values.Length >= 3 ? Matrix2D.Translation(-values[1], -values[2]) * Matrix2D.Rotation(values[0]) * Matrix2D.Translation(values[1], values[2]) : Matrix2D.Rotation(values[0]); break;
                case "skewX" when values.Length >= 1: matrix = new(1, 0, Math.Tan(values[0] * Math.PI / 180), 1, 0, 0); break;
                case "skewY" when values.Length >= 1: matrix = new(1, Math.Tan(values[0] * Math.PI / 180), 0, 1, 0, 0); break;
            }
            result = matrix * result;
        }
        return result;
    }
    private static string Transform(Matrix2D m) => $"matrix({F(m.M11)} {F(m.M12)} {F(m.M21)} {F(m.M22)} {F(m.DX)} {F(m.DY)})";
    private static string F(double number) => number.ToString("0.######", CultureInfo.InvariantCulture);
    [GeneratedRegex(@"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?")] private static partial Regex NumberRegex();
    [GeneratedRegex(@"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)")] private static partial Regex TransformRegex();
}
