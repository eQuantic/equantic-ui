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
        // A stop of the Tab order itself, before its cells': the keys it takes scroll it, and a scroller
        // a keyboard cannot reach scrolls for nobody who only has a keyboard. Unless it declines, for a
        // component that composes it and keeps the keyboard.
        element.RawAttributes = new Dictionary<string, string> { ["role"] = "group" };
        if (viewport.CanRequestFocus) element.RawAttributes["tabindex"] = "0";
        if (viewport.Label is { Length: > 0 } label) element.RawAttributes["aria-label"] = label;

        for (var marks = 0; marks < 2; marks++)
        {
            for (var band = 0; band < TableBands; band++)
                element.Children.Add(LowerTableBand(viewport, band, marks == 1, pinnedRows, pinnedColumns,
                    pinnedAcross, pinnedDown));
        }
        element.Children.Add(LowerTableScrollbar());
        element.Children.Add(LowerTableScrollbar());
        return element;
    }

    /// <summary>One band: a clipping box at its edges, and the layer its cells sit on.</summary>
    private HtmlElement LowerTableBand(TableViewport viewport, int band, bool marks, int pinnedRows,
        int pinnedColumns, float pinnedAcross, float pinnedDown)
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

        var offset = viewport.Scroll.Offset;
        foreach (var cell in viewport.Cells)
        {
            if (cell.Mark != marks) continue;
            var inPinnedRow = cell.Row < pinnedRows;
            var inPinnedColumn = cell.Column < pinnedColumns;
            if ((inPinnedColumn ? 1 : 0) + (inPinnedRow ? 2 : 0) != band) continue;
            layer.Children.Add(LowerTableCell(viewport, cell, inPinnedRow, inPinnedColumn, pinnedAcross,
                pinnedDown, offset));
        }
        return box;
    }

    /// <summary>A cell, or a mark, placed in its band's layer over the rows and columns it covers: from the
    /// band's corner, less the scroll on each axis the band moves along, in double until it is written. The
    /// grid's far rows are past what a float holds, their distance from the window's corner never is, so a
    /// mark and its cell round alike. A mark's glide is the runtime's after-pass (TS twin: lowerTableCell,
    /// and <c>table-viewports.ts</c>): the server's first pass has nothing to glide from.</summary>
    private HtmlElement LowerTableCell(TableViewport viewport, TableViewCell cell, bool inPinnedRow,
        bool inPinnedColumn, float pinnedAcross, float pinnedDown, TableOffset offset)
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
        return Decorate(cell, wrapper)!;
    }

    /// <summary>A scrollbar's track and thumb, hidden: the runtime shows and places them once it knows
    /// the window.</summary>
    private static HtmlElement LowerTableScrollbar()
    {
        var track = new RealizedElement("div")
        {
            Style = new HtmlStyle
            {
                Display = Display.None,
                Position = Position.Absolute,
                Background = "var(--eq-color-scrollbar-track)",
            },
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
