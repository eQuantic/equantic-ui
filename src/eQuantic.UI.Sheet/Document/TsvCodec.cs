namespace eQuantic.UI.Sheet;

/// <summary>
/// The clipboard dialect every spreadsheet already speaks: tab-separated cells, newline-separated
/// rows, Excel's quoting for cells that CONTAIN tabs, newlines or quotes. This is what makes
/// copy/paste between this component and Excel/Google Sheets just work.
/// </summary>
public static class TsvCodec
{
    public static string Serialize(SheetDocument document, SheetRange range)
    {
        var rows = new List<string>();
        for (var row = range.TopRow; row <= range.BottomRow; row++)
        {
            var cells = new List<string>();
            for (var col = range.LeftCol; col <= range.RightCol; col++)
                cells.Add(Escape(document.GetCell(new CellRef(row, col))));
            rows.Add(string.Join("\t", cells));
        }
        return string.Join("\n", rows);
    }

    private static string Escape(string value)
    {
        if (value.IndexOf('\t') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('"') < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Rows of cells. A single unquoted trailing newline (every real clipboard has one)
    /// is not a row.</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = "";
        var quoted = false;
        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell += '"'; i += 2; continue; }
                    quoted = false;
                    i++;
                    continue;
                }
                cell += ch;
                i++;
                continue;
            }
            if (ch == '"' && cell.Length == 0) { quoted = true; i++; continue; }
            if (ch == '\t') { row.Add(cell); cell = ""; i++; continue; }
            if (ch == '\r') { i++; continue; }
            if (ch == '\n')
            {
                row.Add(cell);
                rows.Add(row);
                row = new List<string>();
                cell = "";
                i++;
                continue;
            }
            cell += ch;
            i++;
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell);
            rows.Add(row);
        }
        return rows;
    }
}
