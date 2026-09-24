namespace eQuantic.UI.Code;

/// <summary>
/// How a document's lines become the ROWS a view draws (docs/CODE-EDITOR-PLAN.md, the shape, §9).
/// <para>
/// A row is a line until something says otherwise: rows of filler that belong to no line (the
/// padding that keeps two sides of a diff level, or the removed lines an inline diff draws between
/// the lines that replaced them), and runs of lines hidden behind one placeholder row (a diff's
/// unchanged region) or behind none (a fold, under the header that stays). The grid places a line on
/// its row before it places anything on a point, so a caret, a selection, a click and a reveal all
/// agree with what the block drew.
/// </para>
/// <para>
/// Built once per document and view, and asked by halving: the view is a list of segments, each a
/// run of lines, a filler or a hidden run, with the row it starts on.
/// </para>
/// </summary>
public sealed class CodeRows
{
    /// <summary>What each segment is.</summary>
    private readonly List<CodeRowKind> _kinds = new();

    /// <summary>The first line a segment covers, or the line a filler stands before.</summary>
    private readonly List<int> _lines = new();

    /// <summary>How many lines a segment covers: none for a filler.</summary>
    private readonly List<int> _lineCounts = new();

    /// <summary>The row a segment starts on.</summary>
    private readonly List<int> _rows = new();

    /// <summary>How many rows a segment takes: none for a run hidden behind no placeholder.</summary>
    private readonly List<int> _rowCounts = new();

    /// <summary>The line of another document a filler shows from, or -1.</summary>
    private readonly List<int> _sources = new();

    /// <summary>
    /// The rows of a document of <paramref name="lineCount"/> lines, with <paramref name="fillers"/>
    /// before the lines they name and <paramref name="collapses"/> hidden. A filler at the same line
    /// as a collapse comes before it. Collapses may not overlap, and a filler may not stand inside
    /// one: either is a view that cannot be drawn, and says so.
    /// </summary>
    public CodeRows(int lineCount, IReadOnlyList<CodeFiller> fillers, IReadOnlyList<CodeCollapse> collapses)
    {
        // No lines is a view too: a diff against nothing is its fillers alone. A document always has
        // a line, and a list of lines need not.
        LineCount = Math.Max(0, lineCount);
        var sortedFillers = fillers.Where(filler => filler.Rows > 0).OrderBy(filler => filler.BeforeLine).ToList();
        var sortedCollapses = collapses.Where(collapse => collapse.LastLine >= collapse.FirstLine)
            .OrderBy(collapse => collapse.FirstLine).ToList();

        var line = 0;
        var row = 0;
        var f = 0;
        var c = 0;
        while (f < sortedFillers.Count || c < sortedCollapses.Count)
        {
            // A filler before the next collapse's first line, or at it, comes first.
            var takeFiller = f < sortedFillers.Count
                && (c >= sortedCollapses.Count || sortedFillers[f].BeforeLine <= sortedCollapses[c].FirstLine);
            var at = takeFiller ? sortedFillers[f].BeforeLine : sortedCollapses[c].FirstLine;
            if (at < line || at > LineCount)
                throw new ArgumentException($"A filler or a collapse at line {at} overlaps a collapse, or lies outside the {LineCount} lines.");
            if (at > line)
            {
                Add(CodeRowKind.Line, line, at - line, row, at - line, -1);
                row += at - line;
                line = at;
            }
            if (takeFiller)
            {
                var filler = sortedFillers[f++];
                Add(CodeRowKind.Filler, filler.BeforeLine, 0, row, filler.Rows, filler.SourceLine);
                row += filler.Rows;
            }
            else
            {
                var collapse = sortedCollapses[c++];
                var last = Math.Min(collapse.LastLine, LineCount - 1);
                var rows = collapse.Placeholder ? 1 : 0;
                Add(CodeRowKind.Placeholder, collapse.FirstLine, last - collapse.FirstLine + 1, row, rows, -1);
                row += rows;
                line = last + 1;
            }
        }
        if (line < LineCount)
        {
            Add(CodeRowKind.Line, line, LineCount - line, row, LineCount - line, -1);
            row += LineCount - line;
        }
        RowCount = row;
    }

    private void Add(CodeRowKind kind, int line, int lineCount, int row, int rowCount, int source)
    {
        _kinds.Add(kind);
        _lines.Add(line);
        _lineCounts.Add(lineCount);
        _rows.Add(row);
        _rowCounts.Add(rowCount);
        _sources.Add(source);
    }

    /// <summary>How many lines the document has.</summary>
    public int LineCount { get; }

    /// <summary>How many rows the view draws.</summary>
    public int RowCount { get; }

    /// <summary>
    /// The row <paramref name="line"/> is drawn on. A hidden line answers its placeholder's row, or,
    /// hidden behind none, the row above it (a fold's header), so a caret that somehow stands there
    /// is still drawn where the reader looks.
    /// </summary>
    public int RowOf(int line)
    {
        if (LineCount == 0) return 0;
        var target = Math.Clamp(line, 0, LineCount - 1);
        var segment = SegmentOfLine(target);
        if (_kinds[segment] == CodeRowKind.Line) return _rows[segment] + (target - _lines[segment]);
        if (_rowCounts[segment] > 0) return _rows[segment];
        return Math.Max(0, _rows[segment] - 1);
    }

    /// <summary>Whether <paramref name="line"/> is drawn on a row of its own.</summary>
    public bool IsVisible(int line)
    {
        if (line < 0 || line >= LineCount) return false;
        return _kinds[SegmentOfLine(line)] == CodeRowKind.Line;
    }

    /// <summary>What row <paramref name="row"/> shows. A row outside the view is clamped into it.</summary>
    public CodeRow RowAt(int row)
    {
        if (RowCount == 0) return new CodeRow(CodeRowKind.Filler, 0);
        var target = Math.Clamp(row, 0, RowCount - 1);
        var segment = SegmentOfRow(target);
        var offset = target - _rows[segment];
        return _kinds[segment] switch
        {
            CodeRowKind.Line => new CodeRow(CodeRowKind.Line, _lines[segment] + offset),
            CodeRowKind.Filler => new CodeRow(CodeRowKind.Filler, _lines[segment], 1,
                _sources[segment] >= 0 ? _sources[segment] + offset : -1),
            _ => new CodeRow(CodeRowKind.Placeholder, _lines[segment], _lineCounts[segment]),
        };
    }

    /// <summary>
    /// The line a press on <paramref name="row"/> lands on: the row's own line, the line a filler
    /// stands before (the last line, for a filler after it), or the first line a placeholder hides.
    /// </summary>
    public int LineAtRow(int row)
    {
        var shown = RowAt(row);
        return Math.Max(0, Math.Min(shown.Line, LineCount - 1));
    }

    /// <summary>The segment that covers <paramref name="line"/>: the last one covering lines that
    /// starts at or before it.</summary>
    private int SegmentOfLine(int line)
    {
        var low = 0;
        var high = _kinds.Count - 1;
        var found = 0;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (_lines[middle] <= line)
            {
                if (_lineCounts[middle] > 0) found = middle;
                low = middle + 1;
            }
            else high = middle - 1;
        }
        // Halving by line lands on the last segment starting at or before the line, which a filler
        // there can be: the covering segment is the last one with lines, at or before it.
        while (found > 0 && (_lineCounts[found] == 0 || _lines[found] > line)) found--;
        while (found + 1 < _kinds.Count && _lineCounts[found + 1] > 0 && _lines[found + 1] <= line) found++;
        return found;
    }

    /// <summary>The segment row <paramref name="row"/> falls in: the last one with rows that starts
    /// at or before it.</summary>
    private int SegmentOfRow(int row)
    {
        var low = 0;
        var high = _kinds.Count - 1;
        var found = 0;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (_rows[middle] <= row)
            {
                if (_rowCounts[middle] > 0) found = middle;
                low = middle + 1;
            }
            else high = middle - 1;
        }
        while (found > 0 && _rowCounts[found] == 0) found--;
        return found;
    }
}
