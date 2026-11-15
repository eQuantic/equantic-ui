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

    /// <summary>
    /// What is left of a scroller's extent from where a viewport starts is worth filling when it is at
    /// least this share of what the scroller shows (web twin: <c>WORTH_FILLING</c> in
    /// <c>table-viewports.ts</c>). Further down a long page, what is left is a sliver, and the viewport
    /// takes the whole extent instead (#1006).
    /// </summary>
    private const float WorthFilling = 0.5f;

    /// <summary>Where each viewport started inside the content of the scroller it took its extent from,
    /// across and down, the last time that scroller laid it out: the next measure reads it before the
    /// viewport is placed, by the scroll model the viewport carries, as <see cref="Logged"/> does.</summary>
    private static readonly ConditionalWeakTable<ITableScrollModel, StrongBox<(float Across, float Down)>> Starts = new();

    /// <summary>The viewports that took the extent of a scroller measuring its content, on each axis:
    /// appended by <see cref="TableExtent"/>, read and taken back by that scroller
    /// (<see cref="OpenTaking"/>). Allocated once for the pass, never per frame.</summary>
    private List<Taking>? _takingAcross, _takingDown;

    /// <summary>How many scrollers on each axis are measuring their content around the node being
    /// measured: a viewport registers only while one is.</summary>
    private int _scrollingAcross, _scrollingDown;

    /// <summary>A viewport that took a scroller's extent: its laid-out node, what the scroller shows and
    /// what the viewport took of it.</summary>
    private readonly record struct Taking(TableViewport Viewport, LayoutNode Node, float Shown, float Took);

    /// <summary>The viewports a scroller is measuring on one axis, from <see cref="Mark"/> on.</summary>
    private readonly record struct TakingScope(bool Across, int Mark);

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
        var width = TableExtent(viewport, result, viewport.Width, constraints.Width, _ctx.WindowWidth, across: true);
        var height = TableExtent(viewport, result, viewport.Height, constraints.Height, _ctx.WindowHeight, across: false);
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
    /// room to give at all, a scroller's, gives what the nearest scroller around shows from where the
    /// viewport starts in its content, or the window's extent: there a table would otherwise be as tall
    /// as its grid, and its own scroll would never engage.
    /// </summary>
    private float TableExtent(TableViewport viewport, LayoutNode node, SizeValue size, AxisConstraint axis, float window, bool across)
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

        if (axis.Visible <= 0)
        {
            LogUnbounded(viewport, across, window, window, fromScroller: false);
            return window;
        }
        var shown = axis.Visible;
        // A measure no scroller is running holds no content to start in: the whole extent, as before.
        if ((across ? _scrollingAcross : _scrollingDown) == 0)
        {
            LogUnbounded(viewport, across, shown, shown, fromScroller: true);
            return shown;
        }

        // Inside a scroller, what it shows from where the viewport started the last time it laid it out,
        // so a table below the top of a page ends where the page's visible part ends (#1006). The scroller
        // reads where it starts now, measures again when that moved, and has the log say what was taken.
        var left = shown - (Starts.TryGetValue(viewport.Scroll, out var starts) ? (across ? starts.Value.Across : starts.Value.Down) : 0);
        var took = left < shown && left >= shown * WorthFilling ? left : shown;
        (across ? _takingAcross ??= [] : _takingDown ??= []).Add(new Taking(viewport, node, shown, took));
        return took;
    }

    /// <summary>Opens what a scroller on one axis measures its content in: the viewports that take its
    /// extent from here on are its own until <see cref="CloseTaking"/>.</summary>
    private TakingScope OpenTaking(bool across)
    {
        if (across) _scrollingAcross++;
        else _scrollingDown++;
        return new TakingScope(across, (across ? _takingAcross : _takingDown)?.Count ?? 0);
    }

    /// <summary>
    /// Reads where each viewport that took the scroller's extent starts in its content, the sum of the
    /// offsets from the viewport's node up to <paramref name="content"/>, and answers whether one
    /// started elsewhere than the measure assumed: the scroller then measures its content again, once,
    /// and the viewport takes what is left from there. A viewport's own extent moves nothing above it,
    /// so the second measure settles it. When one moved its entries go, since the second measure
    /// writes its own; otherwise they stay for <see cref="CloseTaking"/> to say what was taken.
    /// </summary>
    private bool StartedElsewhere(TakingScope scope, LayoutNode content)
    {
        var taking = scope.Across ? _takingAcross : _takingDown;
        if (taking is null || taking.Count <= scope.Mark) return false;
        var moved = false;
        for (var i = scope.Mark; i < taking.Count; i++)
        {
            var entry = taking[i];
            // A node measured for a size and thrown away, as an intrinsic pass does, is not under the
            // content, and says nothing about where the viewport stands.
            if (StartIn(entry.Node, content, scope.Across) is not { } start) continue;
            var starts = Starts.GetValue(entry.Viewport.Scroll, _ => new StrongBox<(float, float)>());
            var before = scope.Across ? starts.Value.Across : starts.Value.Down;
            if (MathF.Abs(before - start) < 0.5f) continue;
            starts.Value = scope.Across ? (start, starts.Value.Down) : (starts.Value.Across, start);
            moved = true;
        }
        if (moved) taking.RemoveRange(scope.Mark, taking.Count - scope.Mark);
        return moved;
    }

    /// <summary>Where <paramref name="node"/> starts inside <paramref name="content"/> on an axis, or null
    /// when it is not under it.</summary>
    private static float? StartIn(LayoutNode node, LayoutNode content, bool across)
    {
        var start = 0f;
        for (var at = node; at is not null; at = at.Parent)
        {
            if (ReferenceEquals(at, content)) return start;
            start += across ? at.Bounds.X : at.Bounds.Y;
        }
        return null;
    }

    /// <summary>Closes the scroller's scope: the development log says what each viewport took, and its
    /// entries go. The last scroller on the axis to close leaves nothing behind.</summary>
    private void CloseTaking(TakingScope scope)
    {
        if (scope.Across) _scrollingAcross--;
        else _scrollingDown--;
        var taking = scope.Across ? _takingAcross : _takingDown;
        if (taking is null) return;
        for (var i = scope.Mark; i < taking.Count; i++)
            LogUnbounded(taking[i].Viewport, scope.Across, taking[i].Took, taking[i].Shown, fromScroller: true);
        if ((scope.Across ? _scrollingAcross : _scrollingDown) == 0) taking.Clear();
        else if (taking.Count > scope.Mark) taking.RemoveRange(scope.Mark, taking.Count - scope.Mark);
    }

    /// <summary>A size of its own, held to the room across as the web holds it (<c>max-width: 100%</c>):
    /// a table wider than its parent puts its scrollbar where nobody sees it. Down, a size is its own on
    /// both targets.</summary>
    private static float Capped(float extent, AxisConstraint axis, bool across) =>
        across && !axis.IsUnbounded ? Math.Min(extent, axis.Max) : extent;

    /// <summary>Says, once per viewport and axis, that the viewport took a visible extent because the
    /// axis it was asked to fill had no bound.</summary>
    private void LogUnbounded(TableViewport viewport, bool across, float took, float shown, bool fromScroller)
    {
        if (_ctx.Log is not { } log) return;
        var axes = Logged.GetValue(viewport.Scroll, _ => new StrongBox<int>());
        var bit = across ? 1 : 2;
        if ((axes.Value & bit) != 0) return;
        axes.Value |= bit;
        log($"A table viewport{(viewport.Label is { Length: > 0 } label ? $" ({label})" : "")} was asked to fill its "
            + $"{(across ? "width" : "height")}, which had no bound; it takes the "
            + (took < shown
                ? $"{took} of the {shown} points the {(fromScroller ? "scroller around it" : "window")} shows that are left from where it starts"
                : $"{took} points the {(fromScroller ? "scroller around it" : "window")} shows")
            + ", and scrolls its grid inside them.");
    }
}
