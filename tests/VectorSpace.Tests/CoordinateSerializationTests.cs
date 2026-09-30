using System.Text.Json;
using System.Text.Json.Nodes;
using VectorSpace.Core;
using VectorSpace.Documents;

internal static class CoordinateSerializationTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Coordinate serialization assertion failed."); }
    public static void Register(Action<string, Action> test)
    {
        test("native coordinates serialize only authored X and Y for anchors handles and contours", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Contours = [new() { Points =
                [new() { Position = new(3, 4), ControlOut = new(7, 8) }, new() { Position = new(20, 10), ControlIn = new(12, 4) }] },
                new() { Points = [new() { Position = new(30, 40) }, new() { Position = new(40, 50) }] }] };
            var document = new DesignDocument { Pages = [new() { Nodes = [n] }] };
            var saved = DocumentJson.Save(document);
            Check(!saved.Contains("\"isFinite\"") && !saved.Contains("\"length\""));
            using var json = JsonDocument.Parse(saved);
            var point = json.RootElement.GetProperty("pages")[0].GetProperty("nodes")[0].GetProperty("contours")[0].GetProperty("points")[0];
            Check(point.GetProperty("position").EnumerateObject().Count() == 2 && point.GetProperty("controlOut").EnumerateObject().Count() == 2);
            var restored = DocumentJson.Load(saved).Pages[0].Nodes[0];
            Check(restored.Contours![0].Points[0].Position == new Vec2(3, 4) && restored.Contours[0].Points[0].Position.Length == 5);
            Check(restored.Contours[0].Points[0].ControlOut == new Vec2(7, 8));
        });
        for (var version = 1; version <= DesignDocument.CurrentFormatVersion; version++)
        {
            var schema = version;
            test("schema " + schema + " coordinates with legacy derived metadata remain readable", () =>
            {
                var n = new DesignNode { Kind = NodeKind.Path, Points = [new() { Position = new(3, 4) }, new() { Position = new(30, 40), ControlIn = new(6, 8) }] };
                var document = new DesignDocument { Pages = [new() { Nodes = [n] }] };
                var json = JsonNode.Parse(DocumentJson.Save(document))!; json["formatVersion"] = schema;
                var points = json["pages"]![0]!["nodes"]![0]!["points"]!;
                // Read-only legacy metadata is not an authored value and must not
                // override the recomputed geometry after loading.
                points[0]!["position"]!["length"] = 999; points[0]!["position"]!["isFinite"] = false;
                points[1]!["controlIn"]!["length"] = 10; points[1]!["controlIn"]!["isFinite"] = true;
                var restored = DocumentJson.Load(json.ToJsonString()); var p = restored.Pages[0].Nodes[0].Points[0].Position;
                Check(p == new Vec2(3, 4) && p.Length == 5 && p.IsFinite);
                var saved = DocumentJson.Save(restored);
                Check(!saved.Contains("\"isFinite\"") && !saved.Contains("\"length\""));
            });
        }
        test("dropping coordinate metadata cannot admit a nonfinite authored position", () =>
        {
            var n = new DesignNode { Kind = NodeKind.Path, Points = [new() { Position = new(double.NaN, 0) }, new() { Position = new(1, 0) }] };
            try { DocumentJson.Validate(new() { Pages = [new() { Nodes = [n] }] }); }
            catch (InvalidDataException) { return; }
            throw new Exception("Invalid authored position was accepted.");
        });
    }
}
