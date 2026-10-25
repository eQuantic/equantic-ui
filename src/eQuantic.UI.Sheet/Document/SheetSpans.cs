using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// One axis of a sheet as a table viewport reads it: the document's rows (or columns), each as tall
/// (or as wide) as the document says, every offset a binary search over the few sized differently
/// rather than a sum over every row before it. Live: the overrides and their running sum are read
/// again when the document's sizes changed, so a resize shows at the next layout.
/// </summary>
public sealed class SheetSpans : TableSpans
{
    private readonly SheetDocument _document;
    private readonly SheetAxis _axis;

    /// <summary>The document's size version the overrides below were read at.</summary>
    private int _readAt;

    private int[] _indices;

    /// <summary>What the overrides before each one add to the default: <c>_added[k]</c> is the sum over
    /// the first <c>k</c>.</summary>
    private double[] _added;

    internal SheetSpans(SheetDocument document, SheetAxis axis)
    {
        _document = document;
        _axis = axis;
        _readAt = -1;
        _indices = [];
        _added = [0];
    }

    public override int Count => _axis == SheetAxis.Rows ? _document.Rows : _document.Cols;

    /// <summary>How far every row (or column) the document has not sized reaches.</summary>
    private double Extent => _axis == SheetAxis.Rows ? SheetDocument.DefaultRowHeight : SheetDocument.DefaultColWidth;

    public override double Total => OffsetOf(Count);

    public override double ExtentOf(int index) =>
        _axis == SheetAxis.Rows ? _document.RowHeight(index) : _document.ColWidth(index);

    public override double OffsetOf(int index)
    {
        Read();
        var clamped = Math.Clamp(index, 0, Count);
        return clamped * Extent + _added[Before(clamped)];
    }

    public override int IndexAt(double offset)
    {
        var count = Count;
        if (count == 0) return 0;
        var low = 0;
        var high = count - 1;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            if (OffsetOf(middle) <= offset) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    /// <summary>The overrides, sorted, with their running sum, read again when the sizes changed.</summary>
    private void Read()
    {
        if (_readAt == _document.SizeVersion) return;
        _readAt = _document.SizeVersion;
        var sizes = _axis == SheetAxis.Rows ? _document.RowHeights : _document.ColWidths;
        var indices = new List<int>(sizes.Keys);
        indices.Sort();
        _indices = indices.ToArray();
        _added = new double[_indices.Length + 1];
        for (var k = 0; k < _indices.Length; k++) _added[k + 1] = _added[k] + (sizes[_indices[k]] - Extent);
    }

    /// <summary>How many overrides stand before <paramref name="index"/>.</summary>
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
