using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The table viewport on the web, as the server writes it before the browser has laid anything out
/// (TS twin: <c>lowerTableViewport</c>). A box that clips, holding four bands of cells (the ones that
/// scroll on both axes, the pinned columns, the pinned rows, the corner), each with a layer the
/// runtime moves by transform as the grid scrolls; the same four bands again for the marks, which take
/// no pointer; and a scrollbar on each axis, hidden until the runtime has measured the window and
/// knows whether the grid overflows it. The offset is the model's: the browser scrolls nothing here.
/// </summary>
internal sealed partial class WebLoweringVisitor
{
    /// <summary>The bands, in paint order: what scrolls on both axes, the pinned columns, the pinned
    /// rows, the corner.</summary>
    private const int TableBands = 4;

    public HtmlElement? Visit(TableViewport viewport, bool? horizontalAxis) => LowerTableViewport(viewport);

    /// <summary>A cell outside a viewport has nothing to be placed against: it is its child.</summary>
    public HtmlElement? Visit(TableViewCell cell, bool? horizontalAxis) => Lower(cell.Child, horizontalAxis);

    private HtmlElement LowerTableViewport(TableViewport viewport)
    {
        var rows = viewport.Rows;
        var columns = viewport.Columns;
        var pinnedRows = Math.Clamp(viewport.PinnedRows, 0, rows.Count);
        var pinnedColumns = Math.Clamp(viewport.PinnedColumns, 0, columns.Count);
        var pinnedAcross = (float)columns.OffsetOf(pinnedColumns);
        var pinnedDown = (float)rows.OffsetOf(pinnedRows);

        var element = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Position = Position.Relative,
                Overflow = "hidden",
                Width = Size(viewport.Width) ?? "100%",
                Height = Size(viewport.Height, vertical: true) ?? "100%",
                MinWidth = "0",
                MinHeight = "0",
                MaxWidth = "100%",
                // It takes the wheel, a press and a finger itself, so it declares itself a target, and
                // the browser pans and zooms nothing under it.
                PointerEvents = "auto",
                TouchAction = "none",
            },
        };
        // A table or a grid reads as rows of cells (TS twin: lowerTableViewport, #1081); a group adds nothing.
        TableSemantics? semantics = viewport.Role == TableRole.Group
            ? null
            : new TableSemantics(viewport.Role == TableRole.Grid, StyleAtomizer.Hash(viewport.Label ?? ""));
        element.RawAttributes = new Dictionary<string, string>
        {
            ["role"] = viewport.Role switch { TableRole.Table => "table", TableRole.Grid => "grid", _ => "group" },
        };
        if (semantics is { } table)
        {
            // The whole grid's count, not the cells built: a reader is told where it is in a million rows.
            element.RawAttributes["aria-rowcount"] = rows.Count.ToString(CultureInfo.InvariantCulture);
            element.RawAttributes["aria-colcount"] = columns.Count.ToString(CultureInfo.InvariantCulture);
            if (table.Grid) element.RawAttributes["aria-multiselectable"] = "true";
        }
        // A stop of the Tab order itself, before its cells': the keys it takes scroll it, and a scroller
        // a keyboard cannot reach scrolls for nobody who only has a keyboard. Unless it declines, for a
        // component that composes it and keeps the keyboard.
        if (viewport.CanRequestFocus) element.RawAttributes["tabindex"] = "0";
        if (viewport.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;

        for (var marks = 0; marks < 2; marks++)
        {
            for (var band = 0; band < TableBands; band++)
                element.Children.Add(LowerTableBand(viewport, band, marks == 1, pinnedRows, pinnedColumns,
                    pinnedAcross, pinnedDown, semantics));
        }
        element.Children.Add(LowerTableScrollbar(semantics is not null));
        element.Children.Add(LowerTableScrollbar(semantics is not null));
        if (semantics is { } owner) element.Children.Add(LowerTableRows(viewport, owner));
        return element;
    }

    /// <summary>What a table or a grid adds for a reader (#1081): whether it is a grid, and the scope its
    /// cells' ids are unique in, the label hashed as a <see cref="Navigable"/> grid's are
    /// (<see cref="NavigableScope"/>).</summary>
    private readonly record struct TableSemantics(bool Grid, string Scope);

    /// <summary>The id a table's cell answers to, which its row's <c>aria-owns</c> names (TS twin:
    /// tableCellId).</summary>
    private static string TableCellId(string scope, int row, int column) => $"eq-tcell-{scope}-{row}-{column}";

    /// <summary>A cell of a table or a grid as a reader meets it (TS twin: describeTableCell): its id, its
    /// role, its column and what it spans, and in a grid whether it is selected and whether it is current.</summary>
    private static void DescribeTableCell(HtmlElement wrapper, TableViewCell cell, TableSemantics table)
    {
        wrapper.Id = TableCellId(table.Scope, cell.Row, cell.Column);
        wrapper.Role = cell.Role switch
        {
            TableCellRole.ColumnHeader => "columnheader",
            TableCellRole.RowHeader => "rowheader",
            _ => table.Grid ? "gridcell" : "cell",
        };
        wrapper.AriaColIndex = cell.Column + 1;
        if (cell.Rows > 1) wrapper.AriaRowSpan = cell.Rows;
        if (cell.Columns > 1) wrapper.AriaColSpan = cell.Columns;
        if (!table.Grid) return;
        if (cell.Role == TableCellRole.Cell) wrapper.AriaSelected = cell.Selected;
        if (cell.Current) wrapper.AriaCurrent = "true";
    }

    /// <summary>The rows of a table or a grid (TS twin: lowerTableRows): one per row that holds built cells,
    /// owning them in column order whichever band draws them, since a row's cells live in the pinned
    /// column's band and the body's. A browser places a cell by its order among the cells its row owns, not
    /// by its <c>aria-colindex</c>, so a header stands over its column only while every row owns the same
    /// columns (TableCellRole says what Safari read when the corner was left out). A box of no size, last among the viewport's children, where the
    /// runtime's after-pass, which finds the bands and the scrollbars by their place, never looks.</summary>
    private static HtmlElement LowerTableRows(TableViewport viewport, TableSemantics table)
    {
        var group = new RealizedElement("div")
        {
            Style = new HtmlStyle { Position = Position.Absolute, Width = "0", Height = "0", Overflow = "hidden" },
            Role = "rowgroup",
        };
        var owned = viewport.Cells.Where(cell => !cell.Mark)
            .GroupBy(cell => cell.Row)
            .OrderBy(row => row.Key);
        foreach (var row in owned)
            group.Children.Add(new RealizedElement("div")
            {
                Role = "row",
                AriaRowIndex = row.Key + 1,
                AriaOwns = string.Join(" ", row.OrderBy(cell => cell.Column)
                    .Select(cell => TableCellId(table.Scope, cell.Row, cell.Column))),
            });
        return group;
    }

    /// <summary>One band: a clipping box at its edges, and the layer its cells sit on.</summary>
    private HtmlElement LowerTableBand(TableViewport viewport, int band, bool marks, int pinnedRows,
        int pinnedColumns, float pinnedAcross, float pinnedDown, TableSemantics? semantics)
    {
        var holdsAcross = band is 1 or 3;
        var holdsDown = band is 2 or 3;
        var box = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Position = Position.Absolute,
                Overflow = "hidden",
                Left = holdsAcross ? "0" : TokenCss.Px(pinnedAcross),
                Top = holdsDown ? "0" : TokenCss.Px(pinnedDown),
                Width = holdsAcross ? TokenCss.Px(pinnedAcross) : null,
                Height = holdsDown ? TokenCss.Px(pinnedDown) : null,
                Right = holdsAcross ? null : "0",
                Bottom = holdsDown ? null : "0",
                PointerEvents = marks ? "none" : null,
            },
        };
        var layer = new RealizedElement("div")
        {
            Style = new HtmlStyle { Position = Position.Absolute, Left = "0", Top = "0" },
        };
        box.Children.Add(layer);
        // A mark is drawn over the cells and is nothing to reach: no press, no Tab stop, nothing a
        // reader lands on, whatever it holds.
        if (marks) (box.RawAttributes ??= new())["inert"] = "";
        // In a table or a grid the bands are boxes and nothing else: the rows own the cells they hold.
        if (semantics is not null)
        {
            box.Role = "none";
            layer.Role = "none";
        }

        var offset = viewport.Scroll.Offset;
        foreach (var cell in viewport.Cells)
        {
            if (cell.Mark != marks) continue;
            var inPinnedRow = cell.Row < pinnedRows;
            var inPinnedColumn = cell.Column < pinnedColumns;
            if ((inPinnedColumn ? 1 : 0) + (inPinnedRow ? 2 : 0) != band) continue;
            layer.Children.Add(LowerTableCell(viewport, cell, inPinnedRow, inPinnedColumn, pinnedAcross,
                pinnedDown, offset, marks ? null : semantics));
        }
        return box;
    }

    /// <summary>A cell, or a mark, placed in its band's layer over the rows and columns it covers: from the
    /// band's corner, less the scroll on each axis the band moves along, in double until it is written. The
    /// grid's far rows are past what a float holds, their distance from the window's corner never is, so a
    /// mark and its cell round alike. A mark's glide is the runtime's after-pass (TS twin: lowerTableCell,
    /// and <c>table-viewports.ts</c>): the server's first pass has nothing to glide from.</summary>
    private HtmlElement LowerTableCell(TableViewport viewport, TableViewCell cell, bool inPinnedRow,
        bool inPinnedColumn, float pinnedAcross, float pinnedDown, TableOffset offset, TableSemantics? semantics)
    {
        var rows = viewport.Rows;
        var columns = viewport.Columns;
        var firstRow = Math.Clamp(cell.Row, 0, rows.Count);
        var firstColumn = Math.Clamp(cell.Column, 0, columns.Count);
        var endRow = Math.Clamp(cell.Row + Math.Max(1, cell.Rows), firstRow, rows.Count);
        var endColumn = Math.Clamp(cell.Column + Math.Max(1, cell.Columns), firstColumn, columns.Count);
        var top = rows.OffsetOf(firstRow);
        var left = columns.OffsetOf(firstColumn);
        var x = inPinnedColumn ? left : left - pinnedAcross - offset.Across;
        var y = inPinnedRow ? top : top - pinnedDown - offset.Down;

        var wrapper = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Display.Grid,
                Position = Position.Absolute,
                Left = TokenCss.Px((float)x),
                Top = TokenCss.Px((float)y),
                Width = TokenCss.Px((float)(columns.OffsetOf(endColumn) - left)),
                Height = TokenCss.Px((float)(rows.OffsetOf(endRow) - top)),
            },
        };
        if (Lower(cell.Child, horizontalAxis: null) is { } child) wrapper.Children.Add(child);
        if (semantics is { } table) DescribeTableCell(wrapper, cell, table);
        return Decorate(cell, wrapper)!;
    }

    /// <summary>A scrollbar's track and thumb, hidden: the runtime shows and places them once it knows
    /// the window.</summary>
    private static HtmlElement LowerTableScrollbar(bool inTable)
    {
        var track = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Display.None,
                Position = Position.Absolute,
                Background = "var(--eq-color-scrollbar-track)",
            },
            // A table's or a grid's children are its rows: a scrollbar is a box among them, and nothing else.
            Role = inTable ? "none" : null,
        };
        track.Children.Add(new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Position = Position.Absolute,
                Background = "var(--eq-color-scrollbar-thumb)",
                BorderRadius = "999px",
            },
        });
        return track;
    }
}
