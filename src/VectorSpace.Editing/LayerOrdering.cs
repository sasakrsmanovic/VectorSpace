namespace VectorSpace.Editing;

/// <summary>Stable layer-order operations. Lists are ordered back-to-front. Each selected run moves
/// over one unselected neighbour for a step; extreme moves use one O(n) stable partition.</summary>
public static class LayerOrdering
{
    public static bool CanMove<T>(IReadOnlyList<T> layers, IReadOnlySet<T> selected, int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var seen = false;
        foreach (var layer in layers)
        {
            var chosen = selected.Contains(layer);
            if (direction > 0 ? !chosen && seen : chosen && seen) return true;
            if (direction > 0 ? chosen : !chosen) seen = true;
        }
        return false;
    }
    public static bool Move<T>(List<T> layers, IReadOnlySet<T> selected, int direction, bool extreme = false)
    {
        ArgumentNullException.ThrowIfNull(layers); ArgumentNullException.ThrowIfNull(selected);
        if (!CanMove(layers, selected, direction)) return false;
        if (extreme)
        {
            var result = new T[layers.Count]; var next = 0;
            // Sending back must not reverse the selected layers or the untouched layers.
            foreach (var layer in layers) if (selected.Contains(layer) == (direction < 0)) result[next++] = layer;
            foreach (var layer in layers) if (selected.Contains(layer) != (direction < 0)) result[next++] = layer;
            for (var i = 0; i < result.Length; i++) layers[i] = result[i];
        }
        else if (direction > 0)
        {
            for (var i = layers.Count - 2; i >= 0; i--)
                if (selected.Contains(layers[i]) && !selected.Contains(layers[i + 1])) (layers[i], layers[i + 1]) = (layers[i + 1], layers[i]);
        }
        else
        {
            for (var i = 1; i < layers.Count; i++)
                if (selected.Contains(layers[i]) && !selected.Contains(layers[i - 1])) (layers[i], layers[i - 1]) = (layers[i - 1], layers[i]);
        }
        return true;
    }
}
