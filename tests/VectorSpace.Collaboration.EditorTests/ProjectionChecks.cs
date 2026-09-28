using System.Text.Json.Nodes;
using VectorSpace.Collaboration;
using VectorSpace.Core;
using VectorSpace.Documents;

internal static class ProjectionChecks
{
    public static int Run()
    {
        var count = 0;
        void Equivalent(string json, SharedSnapshot? baseline = null)
        {
            var expected = ProjectionOracle.FromJson(json, baseline);
            var actual = DocumentProjection.FromJson(json, baseline);
            if (actual.Cells.Count != expected.Cells.Count || expected.Cells.Any(p => actual.Value(p.Key) != p.Value))
                throw new InvalidOperationException("Retained projection differs from the original JSON oracle.");
            if (DocumentProjection.ToJson(actual) != DocumentProjection.ToJson(expected)) throw new InvalidOperationException("Projection reconstructed different native JSON.");
            count++;
        }
        var document = SampleDocument.Create();
        var baseline = DocumentProjection.FromDocument(document);
        Equivalent(DocumentJson.Save(document)); Equivalent(DocumentJson.Save(document), baseline);
        var first = document.Pages[0].Nodes[0];
        first.Name = "Escaped \"line\"\nZażółć 😀 <>& \u2028";
        first.Rotation = -123.456789012345; first.Opacity = .000000000001;
        Equivalent(DocumentJson.Save(document), baseline);
        Equivalent(JsonNode.Parse(DocumentJson.Save(document))!.ToJsonString(new() { WriteIndented = true }), baseline);
        first.Add(new() { Id = "inserted", Name = "Nested", Fills = [new() { Kind = FillKind.LinearGradient }] });
        Equivalent(DocumentJson.Save(document), baseline);
        first.Children.Reverse(); Equivalent(DocumentJson.Save(document), baseline);
        first.Children.RemoveAt(0); Equivalent(DocumentJson.Save(document), baseline);
        var random = new Random(13249);
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var layer = first.Children[random.Next(first.Children.Count)];
            layer.X = random.NextDouble() * 1000; layer.Name = "Sample " + iteration + " é";
            if (iteration % 3 == 0) first.Children.Reverse();
            var json = DocumentJson.Save(document); Equivalent(json, baseline);
            baseline = DocumentProjection.FromJson(json, baseline);
        }
        var unchanged = DocumentProjection.FromJson(DocumentJson.Save(document), baseline);
        foreach (var (key, value) in baseline.Cells)
        {
            if (!ReferenceEquals(value, unchanged.Cells[key])) throw new InvalidOperationException("An unchanged cell value was reallocated.");
            if (!unchanged.Cells.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup) || !lookup.TryGetValue(key.AsSpan(), out var actualKey, out _) || !ReferenceEquals(key, actualKey))
                throw new InvalidOperationException("An unchanged cell address was reallocated.");
        }
        count++;
        try { DocumentProjection.FromJson("{\"id\":\"a\",\"id\":\"b\"}"); throw new InvalidOperationException("Duplicate property accepted."); }
        catch (InvalidDataException) { count++; }
        return count;
    }
}
