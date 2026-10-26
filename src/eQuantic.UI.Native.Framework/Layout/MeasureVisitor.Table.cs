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
            var element = at?.ChildAt(i);
            var place = PlaceOf(cell, rows, columns);
            if (cell.Mark) place = Glided(place, cell.Glide, element);
            var laid = Measure(cell, LayoutConstraints.Of(place.Width, place.Height), _ctx, element);
            // Where it stands relative to the window, computed in double: the grid's far end is past
            // what a float holds exactly, the distance from the window's corner never is.
            laid.Bounds = laid.Bounds with
            {
                X = (float)(cell.Column < pinnedColumns ? place.Left : place.Left - offset.Across),
                Y = (float)(cell.Row < pinnedRows ? place.Top : place.Top - offset.Down),
            };
            result.Adopt(laid);
        }
        return result;
    }

    /// <summary>The rectangle a cell covers in its grid, the rows and columns it spans held inside it.</summary>
    private static TablePlace PlaceOf(TableViewCell cell, TableSpans rows, TableSpans columns)
    {
        var firstRow = Math.Clamp(cell.Row, 0, rows.Count);
        var firstColumn = Math.Clamp(cell.Column, 0, columns.Count);
        var endRow = Math.Clamp(cell.Row + Math.Max(1, cell.Rows), firstRow, rows.Count);
        var endColumn = Math.Clamp(cell.Column + Math.Max(1, cell.Columns), firstColumn, columns.Count);
        var left = columns.OffsetOf(firstColumn);
        var top = rows.OffsetOf(firstRow);
        return new TablePlace(left, top, (float)(columns.OffsetOf(endColumn) - left),
            (float)(rows.OffsetOf(endRow) - top));
    }

    /// <summary>
    /// Where a gliding mark is laid out this frame (<see cref="TableViewCell.Glide"/>, web twin: the mark's
    /// transition in <c>lowerTableCell</c>): its four edges glide through the frame's transition clock in the
    /// grid's coordinates, and it is measured at the size it has reached, as Flutter's
    /// <c>AnimatedPositioned</c> lays its child out, so an outline keeps its thickness and what the mark holds
    /// keeps its own size on the way. The scroll is never glided: it is taken out after, so a grid that scrolls
    /// carries the mark at once. The clock snaps under reduced motion and mounts a mark it never saw where it
    /// stands. A mark with no glide goes through the same clock with no specification, which snaps it and
    /// FORGETS its tracks: kept, a mark whose glide was turned off and on again glided back from the rectangle
    /// it stood over when it was turned off.
    /// </summary>
    private TablePlace Glided(TablePlace place, MotionSpec? glide, Element? at)
    {
        if (_ctx.Transitions is not { } clock) return place;
        TransitionSpec? spec = glide is { } motion
            ? new TransitionSpec(StyleChannels.All, motion.DurationMs) { Easing = motion.Curve }
            : null;
        var (time, reduced) = (_ctx.TimeMs, _ctx.ReducedMotion);
        var left = clock.Resolve(at, GlideChannel.Left, (float)place.Left, time, spec, reduced);
        var top = clock.Resolve(at, GlideChannel.Top, (float)place.Top, time, spec, reduced);
        var width = clock.Resolve(at, GlideChannel.Width, place.Width, time, spec, reduced);
        var height = clock.Resolve(at, GlideChannel.Height, place.Height, time, spec, reduced);
        // Standing where it is laid out, the mark keeps the grid's double: a float holds no far row exactly.
        return new TablePlace(left == (float)place.Left ? place.Left : left, top == (float)place.Top ? place.Top : top,
            width, height);
    }

    /// <summary>A rectangle of the grid: where it starts, in double as the grid's extents are, and its size.</summary>
    private readonly record struct TablePlace(double Left, double Top, float Width, float Height);

    /// <summary>
    /// How far the viewport reaches on one axis. A fixed size is its own and a window-relative one is
    /// the window's less its inset; any other takes the room the axis has, as Flutter's viewport takes
    /// its constraints, because a window onto a grid is never as large as the grid. An axis with no
    /// room to give at all, a scroller's, gives the visible extent of the nearest scroller around, or
    /// the window's: there a table would otherwise be as tall as its grid, and its own scroll would
    /// never engage.
    /// </summary>
    private float TableExtent(TableViewport viewport, SizeValue size, AxisConstraint axis, float window, bool across)
    {
        switch (size.Kind)
        {
            case SizeKind.Fixed:
                return Capped(size.Value, axis, across);
            case SizeKind.WindowMinus:
                return Capped(WindowSize(size, window), axis, across);
        }
        // Room to give, even from a parent that sizes from its content: the viewport takes it, as the
        // web's 100% does.
        if (!axis.IsUnbounded) return axis.Max;

        var fromScroller = axis.Visible > 0;
        var visible = fromScroller ? axis.Visible : window;
        LogUnbounded(viewport, across, visible, fromScroller);
        return visible;
    }

    /// <summary>A size of its own, held to the room across as the web holds it (<c>max-width: 100%</c>):
    /// a table wider than its parent puts its scrollbar where nobody sees it. Down, a size is its own on
    /// both targets.</summary>
    private static float Capped(float extent, AxisConstraint axis, bool across) =>
        across && !axis.IsUnbounded ? Math.Min(extent, axis.Max) : extent;

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
