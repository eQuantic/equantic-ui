namespace eQuantic.UI.Code;

/// <summary>
/// Where one line's text elements begin, MEASURED in a proportional face: the measured grid's answer to
/// <see cref="CodeLineCells"/>, which counts cells of a monospaced one. An advance is the width of the
/// line's text up to an element's boundary, measured whole by the view's own measurer, so the kerning,
/// the ligatures and the shaping the platform applies when it draws the line are in it: a sum of
/// characters measured one by one would put a caret beside the glyph it stands after.
/// <para>
/// Measured only where asked and kept: a caret asks one boundary, a band two and a press the few a
/// binary search visits, so a long line in a formula bar costs a handful of measurements per build rather
/// than one per character.
/// </para>
/// </summary>
internal sealed class CodeLineAdvances
{
    private readonly CodeLineCells _cells;
    private readonly Func<string, float> _measure;

    /// <summary>Each boundary's advance once measured, the line's start first; NaN until then.</summary>
    private readonly float[] _advances;

    public CodeLineAdvances(CodeLineCells cells, Func<string, float> measure)
    {
        _cells = cells;
        _measure = measure;
        _advances = new float[cells.Count + 1];
        for (var i = 1; i < _advances.Length; i++) _advances[i] = float.NaN;
    }

    /// <summary>The line this measures.</summary>
    public string Text => _cells.Text;

    /// <summary>The measurer the advances were taken with: another one measures another face.</summary>
    public Func<string, float> Measure => _measure;

    /// <summary>How far from the line's start the boundary before element <paramref name="index"/> is, the
    /// line's whole width at <see cref="CodeLineCells.Count"/>.</summary>
    public float At(int index)
    {
        var advance = _advances[index];
        if (!float.IsNaN(advance)) return advance;
        var column = index < _cells.Count ? _cells.ElementAt(index).Start : _cells.Text.Length;
        advance = _measure(_cells.Text.Substring(0, column));
        _advances[index] = advance;
        return advance;
    }

    /// <summary>The advance a caret before <paramref name="column"/> stands at: a column inside an element,
    /// which no caret should hold, counts as the element's start, and past the end is the line's width.</summary>
    public float Of(int column)
    {
        if (column <= 0) return 0;
        if (column >= _cells.Text.Length) return At(_cells.Count);
        return At(_cells.IndexOf(column));
    }

    /// <summary>
    /// The column nearest to <paramref name="advance"/>, always on an element's boundary: a point in the
    /// left half of an element lands before it, in the right half after it, as
    /// <see cref="CodeLineCells.ColumnAt"/> lands on cells. Past the end, the end.
    /// </summary>
    public int ColumnAt(float advance)
    {
        if (advance <= 0 || _cells.Count == 0) return 0;
        var after = FirstEndingPast(advance);
        if (after < 0) return _cells.Text.Length;
        var element = _cells.ElementAt(after);
        var from = At(after);
        var to = At(after + 1);
        return advance - from <= (to - from) / 2f ? element.Start : element.End;
    }

    /// <summary>The column of the text element drawn at <paramref name="advance"/>, or -1 past the line's
    /// end: what a pointer resting there is over.</summary>
    public int ColumnUnder(float advance)
    {
        if (advance < 0 || _cells.Count == 0) return -1;
        var under = FirstEndingPast(advance);
        return under < 0 ? -1 : _cells.ElementAt(under).Start;
    }

    /// <summary>The first element whose end lies past <paramref name="advance"/>, or -1 when the line ends
    /// before it: a binary search, because the advances only grow.</summary>
    private int FirstEndingPast(float advance)
    {
        var low = 0;
        var high = _cells.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (At(middle + 1) > advance)
            {
                found = middle;
                high = middle - 1;
            }
            else low = middle + 1;
        }
        return found;
    }
}
