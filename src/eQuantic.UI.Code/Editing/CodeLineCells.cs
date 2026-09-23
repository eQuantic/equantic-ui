using System.Globalization;

namespace eQuantic.UI.Code;

/// <summary>
/// Where one line of code lands on the grid, element by element. A document COLUMN is an offset in
/// the line's UTF-16 text, and a CELL is one advance of the mono face on screen. They are the same
/// number only while every character is one unit long and one cell wide, which a tab, a wide
/// character, an emoji or a combining mark is not:
/// <list type="bullet">
/// <item>a tab runs to the next stop, a multiple of the indent width;</item>
/// <item>an East Asian wide or fullwidth character, and an emoji, takes two cells;</item>
/// <item>a text element (an extended grapheme cluster, UAX #29) is never split: its marks, joiners,
/// modifiers and the second half of a surrogate pair take no cell of their own, and no caret stops
/// inside it.</item>
/// </list>
/// The engine places carets and selections through this, the block draws the cells it names, and a
/// click is its inverse, so the three agree (defect 12 of <c>docs/CODE-EDITOR-PLAN.md</c>).
/// </summary>
public sealed class CodeLineCells
{
    /// <summary>Where each element begins, and the line's length last.</summary>
    private readonly int[] _columns;

    /// <summary>The cell each element begins at, and the line's width last.</summary>
    private readonly int[] _cells;

    public CodeLineCells(string text, int tabSize)
    {
        Text = text;
        TabSize = Math.Max(1, tabSize);
        var starts = StringInfo.ParseCombiningCharacters(text);
        _columns = new int[starts.Length + 1];
        _cells = new int[starts.Length + 1];
        var cell = 0;
        for (var i = 0; i < starts.Length; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            _columns[i] = start;
            _cells[i] = cell;
            cell += ElementWidth(text, start, end, cell);
        }
        _columns[starts.Length] = text.Length;
        _cells[starts.Length] = cell;
    }

    /// <summary>The line this maps.</summary>
    public string Text { get; }

    /// <summary>How many cells apart the tab stops are.</summary>
    public int TabSize { get; }

    /// <summary>How many cells the whole line takes.</summary>
    public int Width => _cells[_cells.Length - 1];

    /// <summary>How many text elements the line has.</summary>
    public int Count => _columns.Length - 1;

    /// <summary>The element at <paramref name="index"/>, its columns and its cells.</summary>
    public CodeCell ElementAt(int index) =>
        new(_columns[index], _columns[index + 1], _cells[index], _cells[index + 1] - _cells[index]);

    /// <summary>
    /// The cell a caret before <paramref name="column"/> sits at. A column inside an element, which
    /// no caret should hold, counts as the element's start, and past the end is the line's width.
    /// </summary>
    public int CellOf(int column)
    {
        if (column <= 0) return 0;
        if (column >= Text.Length) return Width;
        return _cells[IndexOf(column)];
    }

    /// <summary>
    /// The column nearest to <paramref name="cell"/>, always on an element's boundary: a point in
    /// the left half of an element lands before it, in the right half after it, so a click on a tab
    /// or a wide character goes to the nearer of its two sides. Past the end, the end.
    /// </summary>
    public int ColumnAt(float cell)
    {
        if (cell <= 0) return 0;
        for (var i = 0; i < Count; i++)
        {
            var from = _cells[i];
            var to = _cells[i + 1];
            if (cell < to) return cell - from <= (to - from) / 2f ? _columns[i] : _columns[i + 1];
        }
        return Text.Length;
    }

    /// <summary>The boundary after <paramref name="column"/>: where → and Delete go.</summary>
    public int Next(int column)
    {
        if (column >= Text.Length) return Text.Length;
        if (column < 0) return 0;
        return _columns[IndexOf(column) + 1];
    }

    /// <summary>The boundary before <paramref name="column"/>: where ← and Backspace go.</summary>
    public int Previous(int column)
    {
        if (column <= 0) return 0;
        if (column > Text.Length) return Text.Length;
        var index = IndexOf(column);
        return _columns[index] == column ? _columns[Math.Max(0, index - 1)] : _columns[index];
    }

    /// <summary>
    /// How many cells <paramref name="text"/> takes, without building its map when it does not have
    /// to: a line of ASCII and tabs, which is nearly every line of code, is counted as it is read,
    /// and only a line with anything else is segmented. A block measures the widest line of the whole
    /// FILE on every build, which is why this is not simply <c>new CodeLineCells(...).Width</c>.
    /// </summary>
    public static int WidthOf(string text, int tabSize)
    {
        var stop = Math.Max(1, tabSize);
        var cell = 0;
        foreach (var c in text)
        {
            if (c == '\t') cell += stop - cell % stop;
            else if (c < (char)0x80) cell++;
            else return new CodeLineCells(text, tabSize).Width;
        }
        return cell;
    }

    /// <summary>The element <paramref name="column"/> falls in: the last one beginning at or before
    /// it, so a column inside an element names that element.</summary>
    public int IndexOf(int column)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_columns[middle] <= column) low = middle;
            else high = middle - 1;
        }
        return Math.Min(low, Count - 1);
    }

    /// <summary>How many cells the element from <paramref name="start"/> to <paramref name="end"/>
    /// takes when it begins at <paramref name="cell"/> (a tab's width depends on where it begins).</summary>
    private int ElementWidth(string text, int start, int end, int cell)
    {
        var first = text[start];
        if (first == '\t') return TabSize - cell % TabSize;
        var codePoint = char.IsHighSurrogate(first) && start + 1 < end
            ? char.ConvertToUtf32(first, text[start + 1])
            : (int)first;
        if (IsWide(codePoint)) return 2;
        // An emoji presentation selector makes the element an emoji, which is drawn two cells wide.
        for (var i = start + 1; i < end; i++)
        {
            if (text[i] == '\uFE0F') return 2;
        }
        return 1;
    }

    /// <summary>
    /// Whether a character takes two cells: the East Asian wide and fullwidth blocks and the emoji
    /// blocks drawn in emoji presentation by default. Neither .NET nor JavaScript exposes the East
    /// Asian Width property, so the blocks are named here, the way terminals and editors name them;
    /// the block draws a wide element in a box two cells wide, so a font that disagrees by a little
    /// cannot move the rest of the line off the grid.
    /// </summary>
    public static bool IsWide(int codePoint) =>
        codePoint is (>= 0x1100 and <= 0x115F)       // Hangul Jamo, leading consonants
            or (>= 0x231A and <= 0x231B)             // watch, hourglass
            or (>= 0x23E9 and <= 0x23EC)             // media controls
            or 0x23F0 or 0x23F3                      // alarm clock, hourglass flowing
            or (>= 0x25FD and <= 0x25FE)             // medium small squares
            or (>= 0x2614 and <= 0x2615)             // umbrella with rain, hot beverage
            or (>= 0x2648 and <= 0x2653)             // the zodiac
            or 0x267F or 0x2693 or 0x26A1            // wheelchair, anchor, high voltage
            or (>= 0x26AA and <= 0x26AB)             // circles
            or (>= 0x26BD and <= 0x26BE)             // soccer ball, baseball
            or (>= 0x26C4 and <= 0x26C5)             // snowman, sun behind cloud
            or 0x26CE or 0x26D4 or 0x26EA            // Ophiuchus, no entry, church
            or (>= 0x26F2 and <= 0x26F3) or 0x26F5 or 0x26FA or 0x26FD
            or 0x2705 or (>= 0x270A and <= 0x270B) or 0x2728 or 0x274C or 0x274E
            or (>= 0x2753 and <= 0x2755) or 0x2757 or (>= 0x2795 and <= 0x2797)
            or 0x27B0 or 0x27BF or (>= 0x2B1B and <= 0x2B1C) or 0x2B50 or 0x2B55
            or (>= 0x2E80 and <= 0x303E)             // CJK radicals, Kangxi, CJK symbols and punctuation
            or (>= 0x3041 and <= 0x33FF)             // kana, Bopomofo, Hangul compatibility jamo, CJK compatibility
            or (>= 0x3400 and <= 0x4DBF)             // CJK Unified Ideographs Extension A
            or (>= 0x4E00 and <= 0x9FFF)             // CJK Unified Ideographs
            or (>= 0xA000 and <= 0xA4CF)             // Yi
            or (>= 0xA960 and <= 0xA97F)             // Hangul Jamo Extended-A
            or (>= 0xAC00 and <= 0xD7A3)             // Hangul syllables
            or (>= 0xF900 and <= 0xFAFF)             // CJK compatibility ideographs
            or (>= 0xFE10 and <= 0xFE19)             // vertical forms
            or (>= 0xFE30 and <= 0xFE6F)             // CJK compatibility forms, small form variants
            or (>= 0xFF00 and <= 0xFF60)             // fullwidth forms
            or (>= 0xFFE0 and <= 0xFFE6)             // fullwidth signs
            or 0x1F004 or 0x1F0CF or 0x1F18E or (>= 0x1F191 and <= 0x1F19A)
            or (>= 0x1F1E6 and <= 0x1F1FF)           // regional indicators: a flag is a pair of them
            or (>= 0x1F200 and <= 0x1F251)           // enclosed ideographic supplement
            or (>= 0x1F300 and <= 0x1F64F)           // symbols and pictographs, emoticons
            or (>= 0x1F680 and <= 0x1F6FF)           // transport and map
            or (>= 0x1F7E0 and <= 0x1F7EB)           // coloured circles and squares
            or (>= 0x1F90C and <= 0x1F9FF)           // supplemental symbols and pictographs
            or (>= 0x1FA70 and <= 0x1FAFF)           // symbols and pictographs extended-A
            or (>= 0x20000 and <= 0x2FFFD)           // CJK Extensions B and on
            or (>= 0x30000 and <= 0x3FFFD);          // plane 3
}
