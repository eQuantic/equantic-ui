using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// An axis whose rows (or columns) reach a default extent, but for the few given their own: a sheet
/// where someone widened column B. The overrides are kept sorted with the running sum of how much each
/// adds to the default, so an offset costs a binary search over the overrides however long the axis.
/// </summary>
public sealed class SparseTableSpans : ITableSpans
{
    private readonly int[] _indices;
    private readonly double[] _extents;

    /// <summary>What the overrides before each one add to the default: <c>_added[k]</c> is the sum over
    /// the first <c>k</c>.</summary>
    private readonly double[] _added;

    public SparseTableSpans(int count, double extent, IReadOnlyDictionary<int, double> extents)
    {
        Count = Math.Max(0, count);
        Extent = Math.Max(0, extent);
        var kept = new List<int>();
        foreach (var index in extents.Keys)
            if (index >= 0 && index < Count) kept.Add(index);
        kept.Sort();
        _indices = kept.ToArray();
        _extents = new double[_indices.Length];
        _added = new double[_indices.Length + 1];
        for (var k = 0; k < _indices.Length; k++)
        {
            _extents[k] = Math.Max(0, extents[_indices[k]]);
            _added[k + 1] = _added[k] + (_extents[k] - Extent);
        }
    }

    public int Count { get; }

    /// <summary>How far every one without an override reaches.</summary>
    public double Extent { get; }

    public double Total => OffsetOf(Count);

    public double ExtentOf(int index)
    {
        var k = Before(index);
        return k < _indices.Length && _indices[k] == index ? _extents[k] : Extent;
    }

    public double OffsetOf(int index)
    {
        var clamped = Math.Clamp(index, 0, Count);
        return clamped * Extent + _added[Before(clamped)];
    }

    public int IndexAt(double offset)
    {
        if (Count == 0) return 0;
        var low = 0;
        var high = Count - 1;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (OffsetOf(middle) <= offset) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    /// <summary>How many overrides stand before <paramref name="index"/>, which is also where one at it
    /// would be.</summary>
    private int Before(int index)
    {
        var low = 0;
        var high = _indices.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_indices[middle] < index) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
