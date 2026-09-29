using SkiaSharp;
using VectorSpace.Core;

namespace VectorSpace.Skia;

public sealed partial class SceneRenderer
{
    private sealed record OperandKey(string Id, SKPath Geometry, Matrix2D Matrix);
    private sealed record BooleanEntry(BooleanKind Kind, OperandKey[] Operands, SKPath Path);
    private readonly Dictionary<string, BooleanEntry> _booleans = new(StringComparer.Ordinal);
    private readonly HashSet<string> _booleanStack = new(StringComparer.Ordinal);
    private int _booleanCapacity = 256;
    public int BooleanCacheCapacity
    {
        get => _booleanCapacity;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _booleanCapacity = value; while (_booleans.Count > value) RemoveBoolean(_booleans.Keys.First()); }
    }
    public long BooleanBuilds { get; private set; }
    public int CachedBooleanCount => _booleans.Count;
    private SKPath BooleanGeometry(DesignNode node)
    {
        if (!_booleanStack.Add(node.Id)) throw new InvalidOperationException("Cyclic Boolean hierarchy.");
        try
        {
            _booleans.TryGetValue(node.Id, out var prior); var unchanged = prior is not null && prior.Kind == node.Boolean;
            var count = 0;
            foreach (var child in node.Children)
            {
                if (!child.Visible) continue;
                var geometry = Geometry(child);
                if (unchanged && (count >= prior!.Operands.Length || prior.Operands[count].Id != child.Id || !ReferenceEquals(prior.Operands[count].Geometry, geometry) || prior.Operands[count].Matrix != child.LocalMatrix)) unchanged = false;
                count++;
            }
            if (unchanged && prior!.Operands.Length == count) return prior.Path;
            var keys = new OperandKey[count]; var index = 0; var result = new SKPath();
            try
            {
                foreach (var child in node.Children)
                {
                    if (!child.Visible) continue;
                    var source = Geometry(child); keys[index] = new(child.Id, source, child.LocalMatrix);
                    using var operand = new SKPath(source); operand.Transform(Matrix(child.LocalMatrix));
                    if (index++ == 0) { result.AddPath(operand); result.FillType = operand.FillType; continue; }
                    using var next = result.Op(operand, Operation(node.Boolean!.Value)) ?? throw new InvalidOperationException("Skia could not combine these operands.");
                    result.Reset(); result.AddPath(next); result.FillType = next.FillType;
                }
                RemoveBoolean(node.Id);
                while (_booleans.Count >= _booleanCapacity) RemoveBoolean(_booleans.Keys.First());
                _booleans[node.Id] = new(node.Boolean!.Value, keys, result); BooleanBuilds++; return result;
            }
            catch { result.Dispose(); throw; }
        }
        finally { _booleanStack.Remove(node.Id); }
    }
    private void RemoveBoolean(string id)
    {
        if (_booleans.Remove(id, out var entry)) entry.Path.Dispose();
    }
    private void ClearBooleanCache()
    {
        foreach (var entry in _booleans.Values) entry.Path.Dispose(); _booleans.Clear();
    }
    private void TrimBooleanCache(HashSet<string> active)
    {
        foreach (var id in _booleans.Keys.Where(id => !active.Contains(id)).ToArray()) RemoveBoolean(id);
    }
    internal static SKPathOp Operation(BooleanKind op) => op switch
    {
        BooleanKind.Union => SKPathOp.Union, BooleanKind.Subtract => SKPathOp.Difference,
        BooleanKind.Intersect => SKPathOp.Intersect, BooleanKind.Exclude => SKPathOp.Xor,
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };
}
