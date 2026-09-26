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

    /// <summary>
    /// How many cells the element from <paramref name="start"/> to <paramref name="end"/> takes when
    /// it begins at <paramref name="cell"/> (a tab's width depends on where it begins). The width is
    /// the CLUSTER's: a skin tone, an emoji presentation selector or a second regional indicator
    /// makes the element an emoji, drawn two cells wide whatever its first character is alone. It was
    /// read from the first character, so ✌🏻 took one cell and drew two.
    /// </summary>
    private int ElementWidth(string text, int start, int end, int cell)
    {
        var first = text[start];
        if (first == '\t') return TabSize - cell % TabSize;
        // A pair, read as one code point; anything else, as the unit it is. A lone high surrogate
        // before a mark is one element to .NET, and reading a pair from the two threw.
        var codePoint = start + 1 < end && char.IsSurrogatePair(first, text[start + 1])
            ? char.ConvertToUtf32(first, text[start + 1])
            : (int)first;
        // An element that BEGINS with a mark (at the start of a line, or after a tab) has no advance
        // of its own when the mark is nonspacing or enclosing: it combines with whatever is drawn
        // before it. A spacing mark has one, by definition. Asked before the wide table, which takes
        // in the marks of the blocks it spans (the voiced sound mark is East Asian Wide).
        if (codePoint >= 0x0300)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) return 0;
        }
        if (IsWide(codePoint)) return 2;
        for (var i = start + 1; i < end; i++)
        {
            var c = text[i];
            if (c == '\uFE0F') return 2;                                  // emoji presentation
            if (c == '\uD83C' && i + 1 < end && text[i + 1] >= '\uDFFB' && text[i + 1] <= '\uDFFF')
                return 2;                                                 // U+1F3FB..U+1F3FF, a skin tone
        }
        if (codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF && end - start >= 4) return 2;   // a flag
        return IsZeroWidth(codePoint) ? 0 : 1;
    }

    /// <summary>
    /// Whether a character takes two cells: East Asian Wide and Fullwidth, which since Unicode 9 takes
    /// in every emoji drawn as an emoji by default. Neither .NET nor JavaScript exposes the property,
    /// so it is written here, as terminals and editors write it, and <c>CellWidthOracleTests</c>
    /// compares it with the SDK's own embedded Bun (<c>Bun.stringWidth</c>) for every assigned
    /// character: a range bridges only characters that never begin an element (unassigned, marks,
    /// private use). The block draws a wide element in a box two cells wide, so a font that
    /// disagrees by a little cannot move the rest of the line off the grid.
    /// </summary>
    public static bool IsWide(int codePoint) =>
        codePoint is
        (>= 0x1100 and <= 0x115F) or (>= 0x231A and <= 0x231B) or (>= 0x2329 and <= 0x232A)
        or (>= 0x23E9 and <= 0x23EC) or 0x23F0 or 0x23F3 or (>= 0x25FD and <= 0x25FE)
        or (>= 0x2614 and <= 0x2615) or (>= 0x2648 and <= 0x2653) or 0x267F or 0x2693 or 0x26A1
        or (>= 0x26AA and <= 0x26AB) or (>= 0x26BD and <= 0x26BE) or (>= 0x26C4 and <= 0x26C5)
        or 0x26CE or 0x26D4 or 0x26EA or (>= 0x26F2 and <= 0x26F3) or 0x26F5 or 0x26FA or 0x26FD
        or 0x2705 or (>= 0x270A and <= 0x270B) or 0x2728 or 0x274C or 0x274E
        or (>= 0x2753 and <= 0x2755) or 0x2757 or (>= 0x2795 and <= 0x2797) or 0x27B0 or 0x27BF
        or (>= 0x2B1B and <= 0x2B1C) or 0x2B50 or 0x2B55 or (>= 0x2E80 and <= 0x303E)
        or (>= 0x3041 and <= 0x31E3) or (>= 0x31EF and <= 0x3247) or (>= 0x3250 and <= 0x4DBF)
        or (>= 0x4E00 and <= 0xA4C6) or (>= 0xA960 and <= 0xA97C) or (>= 0xAC00 and <= 0xD7A3)
        or (>= 0xF900 and <= 0xFAD9) or (>= 0xFE10 and <= 0xFE6B) or (>= 0xFF01 and <= 0xFF60)
        or (>= 0xFFE0 and <= 0xFFE6) or (>= 0x16FE0 and <= 0x16FE3) or (>= 0x17000 and <= 0x187F7)
        or (>= 0x18800 and <= 0x18CD5) or (>= 0x18D00 and <= 0x18D08)
        or (>= 0x1AFF0 and <= 0x1B2FB) or 0x1F004 or 0x1F0CF or 0x1F18E
        or (>= 0x1F191 and <= 0x1F19A) or (>= 0x1F200 and <= 0x1F320)
        or (>= 0x1F32D and <= 0x1F335) or (>= 0x1F337 and <= 0x1F37C)
        or (>= 0x1F37E and <= 0x1F393) or (>= 0x1F3A0 and <= 0x1F3CA)
        or (>= 0x1F3CF and <= 0x1F3D3) or (>= 0x1F3E0 and <= 0x1F3F0) or 0x1F3F4
        or (>= 0x1F3F8 and <= 0x1F43E) or 0x1F440 or (>= 0x1F442 and <= 0x1F4FC)
        or (>= 0x1F4FF and <= 0x1F53D) or (>= 0x1F54B and <= 0x1F54E)
        or (>= 0x1F550 and <= 0x1F567) or 0x1F57A or (>= 0x1F595 and <= 0x1F596) or 0x1F5A4
        or (>= 0x1F5FB and <= 0x1F64F) or (>= 0x1F680 and <= 0x1F6C5) or 0x1F6CC
        or (>= 0x1F6D0 and <= 0x1F6D2) or (>= 0x1F6D5 and <= 0x1F6D7)
        or (>= 0x1F6DC and <= 0x1F6DF) or (>= 0x1F6EB and <= 0x1F6EC)
        or (>= 0x1F6F4 and <= 0x1F6FC) or (>= 0x1F7E0 and <= 0x1F7F0)
        or (>= 0x1F90C and <= 0x1F93A) or (>= 0x1F93C and <= 0x1F945)
        or (>= 0x1F947 and <= 0x1F9FF) or (>= 0x1FA70 and <= 0x1FA88)
        or (>= 0x1FA90 and <= 0x1FABD) or (>= 0x1FABF and <= 0x1FAC5)
        or (>= 0x1FACE and <= 0x1FADB) or (>= 0x1FAE0 and <= 0x1FAE8)
        or (>= 0x1FAF0 and <= 0x1FAF8) or (>= 0x20000 and <= 0x33479);

    /// <summary>
    /// Whether a character takes NO cell: the zero-width space and joiners, the direction marks, the
    /// word joiner and invisible operators, the byte-order mark, the soft hyphen, and the format
    /// characters that are drawn only as part of what follows them. Compared with the same oracle as
    /// <see cref="IsWide"/>.
    /// </summary>
    public static bool IsZeroWidth(int codePoint) =>
        codePoint is
        0xAD or (>= 0x600 and <= 0x605) or 0x6DD or 0x70F or 0x8E2 or (>= 0x200B and <= 0x200F)
        or (>= 0x2060 and <= 0x2064) or 0xFEFF or (>= 0xE0001 and <= 0xE007F);
}
