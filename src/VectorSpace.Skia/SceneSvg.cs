using SkiaSharp;
using VectorSpace.Core;
using VectorSpace.Documents;
using VectorSpace.Editing;

namespace VectorSpace.Skia;

public static class SceneSvg
{
    /// <summary>Renderer-aware export bakes only a temporary copy of live Boolean geometry and
    /// provides exact aligned-stroke regions. The authored scene and history are never modified.</summary>
    public static string Export(SceneRenderer renderer, IEnumerable<DesignNode> roots, RectD bounds)
    {
        var copies = roots.Select(n => { var clone = DocumentJson.CloneNode(n); clone.Parent = n.Parent; Bake(clone); return clone; }).ToArray();
        return SvgFormat.Export(copies, bounds, (node, index) => ShapePathSvg.Commands(NativeShapeGeometry.Capture(renderer.StrokeGeometry(node, index))));
        void Bake(DesignNode n)
        {
            if (n.IsBoolean) { var data = LiveBooleanOperations.Capture(renderer.Geometry(n)); LiveBooleanOperations.SetPath(n, data.Commands, data.Rule); }
            else foreach (var child in n.Children) Bake(child);
        }
    }
}
