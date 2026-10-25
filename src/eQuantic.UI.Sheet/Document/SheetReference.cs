namespace eQuantic.UI.Sheet;

/// <summary>
/// What a person may type into the name box to go somewhere, read as Excel reads it: a cell (<c>B7</c>), a
/// range (<c>B7:D9</c>), whole rows (<c>3:5</c>) or whole columns (<c>B:D</c>), the <c>$</c> anchors a
/// formula writes ignored, letters in either case. Anything that names no cell of the grid, a row 0, a
/// column past the grid's last, a name the sheet does not define (names come with slice 10), is refused.
/// </summary>
public static class SheetReference
{
    /// <summary>The last row Excel addresses, and so the most digits a row may have before it is refused
    /// without counting further.</summary>
    private const int MaxRows = 1_048_576;

    /// <summary>
    /// Reads <paramref name="text"/> against a grid of <paramref name="rows"/> by <paramref name="cols"/>,
    /// into the range it names, the top-left first. Answers false, with the whole first cell, for text that
    /// names nothing in the grid.
    /// </summary>
    public static bool TryParse(string text, int rows, int cols, out SheetRange range)
    {
        range = new SheetRange(new CellRef(0, 0));
        var trimmed = text.Trim().ToUpperInvariant();
        if (trimmed.Length == 0) return false;
        var colon = trimmed.IndexOf(':');
        var first = colon < 0 ? trimmed : trimmed.Substring(0, colon);
        var second = colon < 0 ? null : trimmed.Substring(colon + 1);

        if (second is null)
        {
            if (!ReadCell(first, out var cell) || !Inside(cell, rows, cols)) return false;
            range = new SheetRange(cell);
            return true;
        }

        if (ReadCell(first, out var from) && ReadCell(second, out var to))
        {
            if (!Inside(from, rows, cols) || !Inside(to, rows, cols)) return false;
            range = Ordered(from, to);
            return true;
        }
        if (ReadRow(first, out var top) && ReadRow(second, out var bottom))
        {
            if (top >= rows || bottom >= rows) return false;
            range = Ordered(new CellRef(top, 0), new CellRef(bottom, cols - 1));
            return true;
        }
        if (ReadColumn(first, out var left) && ReadColumn(second, out var right))
        {
            if (left >= cols || right >= cols) return false;
            range = Ordered(new CellRef(0, left), new CellRef(rows - 1, right));
            return true;
        }
        return false;
    }

    private static SheetRange Ordered(CellRef a, CellRef b) => new(
        new CellRef(Math.Min(a.Row, b.Row), Math.Min(a.Col, b.Col)),
        new CellRef(Math.Max(a.Row, b.Row), Math.Max(a.Col, b.Col)));

    private static bool Inside(CellRef cell, int rows, int cols) => cell.Row < rows && cell.Col < cols;

    /// <summary>A cell: a column, then a row, each with an optional <c>$</c> before it.</summary>
    private static bool ReadCell(string text, out CellRef cell)
    {
        cell = new CellRef(0, 0);
        var at = 0;
        if (!Letters(text, ref at, out var col)) return false;
        if (!Digits(text, ref at, out var row)) return false;
        if (at != text.Length) return false;
        cell = new CellRef(row, col);
        return true;
    }

    private static bool ReadRow(string text, out int row)
    {
        var at = 0;
        return Digits(text, ref at, out row) && at == text.Length;
    }

    private static bool ReadColumn(string text, out int col)
    {
        var at = 0;
        return Letters(text, ref at, out col) && at == text.Length;
    }

    /// <summary>An optional <c>$</c> and one to three letters, as a column counted from 0.</summary>
    private static bool Letters(string text, ref int at, out int col)
    {
        col = -1;
        if (at < text.Length && text[at] == '$') at++;
        var start = at;
        var number = 0;
        while (at < text.Length && text[at] >= 'A' && text[at] <= 'Z')
        {
            if (at - start == 3) return false;
            number = number * 26 + (text[at] - 'A' + 1);
            at++;
        }
        if (at == start) return false;
        col = number - 1;
        return true;
    }

    /// <summary>An optional <c>$</c> and a row number from 1, as a row counted from 0.</summary>
    private static bool Digits(string text, ref int at, out int row)
    {
        row = -1;
        if (at < text.Length && text[at] == '$') at++;
        var start = at;
        var number = 0;
        while (at < text.Length && text[at] >= '0' && text[at] <= '9')
        {
            number = number * 10 + (text[at] - '0');
            if (number > MaxRows) return false;
            at++;
        }
        if (at == start || number == 0) return false;
        row = number - 1;
        return true;
    }
}
