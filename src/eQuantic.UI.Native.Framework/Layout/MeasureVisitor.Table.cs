using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The table viewport: a window onto a grid larger than any screen, its cells placed by the extents
/// of the rows and columns they cover and shifted by the scroll its model keeps, the leading bands held
/// still. Measuring it again with a new offset lays the same tree out again, which is how a scroll
/// step on Photon moves the cells without building them.
/// </summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>The viewports whose unbounded axes the development log has already named, by the scroll
    /// model each carries, which lives as long as the component that owns it.</summary>
    private static readonly ConditionalWeakTable<ITableScrollModel, StrongBox<int>> Logged = new();

    public LayoutNode Visit(TableViewport node, MeasureState s) => MeasureTableViewport(node, s.Constraints, s.At);

    /// <summary>
    /// A cell takes the box it is offered: a viewport offers the rows and columns it covers, and its
    /// child is stretched to that box. Offered no bound, as outside a viewport, it takes its child's.
    /// </summary>
    public LayoutNode Visit(TableViewCell node, MeasureState s)
    {
        var result = _ctx.Node(node);
        var box = s.Constraints;
        var child = Measure(node.Child, box.Stretched(StretchKind.Flex, StretchKind.Flex), _ctx, s.At?.ChildAt(0));
        result.Adopt(child);
        result.Bounds = new Rect(0, 0,
            box.Width.IsUnbounded ? child.Bounds.Width : box.MaxWidth,
            box.Height.IsUnbounded ? child.Bounds.Height : box.MaxHeight);
        return result;
    }

    private LayoutNode MeasureTableViewport(TableViewport viewport, LayoutConstraints constraints, Element? at)
    {
        var result = _ctx.Node(viewport);
        var rows = viewport.Rows;
        var columns = viewport.Columns;
        var width = TableExtent(viewport, viewport.Width, constraints.Width, _ctx.WindowWidth, across: true);
        var height = TableExtent(viewport, viewport.Height, constraints.Height, _ctx.WindowHeight, across: false);
        result.Bounds = new Rect(0, 0, width, height);

        var pinnedRows = Math.Clamp(viewport.PinnedRows, 0, rows.Count);
        var pinnedColumns = Math.Clamp(viewport.PinnedColumns, 0, columns.Count);
        viewport.Scroll.Arrange(new Size(width, height), columns.Total, rows.Total,
            columns.OffsetOf(pinnedColumns), rows.OffsetOf(pinnedRows), _ctx.ReducedMotion);
        viewport.Scroll.Apply(viewport.Request, rows, columns, pinnedRows, pinnedColumns);
        var offset = viewport.Scroll.Offset;

        for (var i = 0; i < viewport.Cells.Count; i++)
        {
            var cell = viewport.Cells[i];
            var laid = MeasureTableCell(cell, rows, columns, at?.ChildAt(i));
            // Where it stands relative to the window, computed in double: the grid's far end is past
            // what a float holds exactly, the distance from the window's corner never is.
            var top = rows.OffsetOf(Math.Clamp(cell.Row, 0, rows.Count));
            var left = columns.OffsetOf(Math.Clamp(cell.Column, 0, columns.Count));
            laid.Bounds = laid.Bounds with
            {
                X = (float)(cell.Column < pinnedColumns ? left : left - offset.Across),
                Y = (float)(cell.Row < pinnedRows ? top : top - offset.Down),
            };
            result.Adopt(laid);
        }
        return result;
    }

    /// <summary>A cell laid out over the rows and columns it covers, held inside the grid.</summary>
    private LayoutNode MeasureTableCell(TableViewCell cell, ITableSpans rows, ITableSpans columns, Element? at)
    {
        var firstRow = Math.Clamp(cell.Row, 0, rows.Count);
        var firstColumn = Math.Clamp(cell.Column, 0, columns.Count);
        var endRow = Math.Clamp(cell.Row + Math.Max(1, cell.Rows), firstRow, rows.Count);
        var endColumn = Math.Clamp(cell.Column + Math.Max(1, cell.Columns), firstColumn, columns.Count);
        var box = LayoutConstraints.Of(
            (float)(columns.OffsetOf(endColumn) - columns.OffsetOf(firstColumn)),
            (float)(rows.OffsetOf(endRow) - rows.OffsetOf(firstRow)));
        return Measure(cell, box, _ctx, at);
    }

    /// <summary>
    /// How far the viewport reaches on one axis. A fixed size is its own and a window-relative one is
    /// the window's less its inset; any other takes the room the axis has, as Flutter's viewport takes
    /// its constraints, because a window onto a grid is never as large as the grid. An axis with no
    /// room to give, a scroller's or a parent's that sizes from its content, gives the visible extent
    /// of the nearest scroller around, or the window's: there a table would otherwise be as tall as its
    /// grid, and its own scroll would never engage.
    /// </summary>
    private float TableExtent(TableViewport viewport, SizeValue size, AxisConstraint axis, float window, bool across)
    {
        switch (size.Kind)
        {
            case SizeKind.Fixed:
                return size.Value;
            case SizeKind.WindowMinus:
                return WindowSize(size, window);
        }
        if (!axis.IsUnbounded && !axis.Indeterminate) return axis.Max;

        var fromScroller = axis.Visible > 0;
        var visible = fromScroller ? axis.Visible : window;
        if (!axis.IsUnbounded) visible = MathF.Min(visible, axis.Max);
        LogUnbounded(viewport, across, visible, fromScroller);
        return visible;
    }

    /// <summary>Says, once per viewport and axis, that the viewport took a visible extent because the
    /// axis it was asked to fill had no bound.</summary>
    private void LogUnbounded(TableViewport viewport, bool across, float visible, bool fromScroller)
    {
        if (_ctx.Log is not { } log) return;
        var axes = Logged.GetValue(viewport.Scroll, _ => new StrongBox<int>());
        var bit = across ? 1 : 2;
        if ((axes.Value & bit) != 0) return;
        axes.Value |= bit;
        log($"A table viewport{(viewport.Label is { Length: > 0 } label ? $" ({label})" : "")} was asked to fill its "
            + $"{(across ? "width" : "height")}, which had no bound; it takes the {visible} points the "
            + $"{(fromScroller ? "scroller around it" : "window")} shows, and scrolls its grid inside them.");
    }
}
