using System.Runtime.CompilerServices;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The table viewport: four bands, each drawn under its own clip with its input narrowed to it (the
/// cells that scroll on both axes, the pinned columns, the pinned rows, the corner), then the marks
/// over each band with no input at all, then a scrollbar on each axis the grid overflows. The region
/// it registers is where the host routes the wheel, a finger and a scrollbar press to its scroll.
/// </summary>
internal sealed partial class EmitVisitor
{
    /// <summary>The last offset and window each viewport's owner was told, by the scroll model it
    /// carries: a viewport reports a change once, not every frame.</summary>
    private static readonly ConditionalWeakTable<ITableScrollModel, TableReport> Reported = new();

    /// <summary>The thickness of a scrollbar's thumb, inside the strip the scroll model keeps.</summary>
    private const float ThumbInset = 2f;

    public Nothing Visit(TableViewport node, EmitState s) { EmitTableViewport(node, s); return Nothing.Value; }

    /// <summary>A cell draws what its child draws, where the viewport placed it.</summary>
    public Nothing Visit(TableViewCell node, EmitState s) { Descend(s); return Nothing.Value; }

    private void EmitTableViewport(TableViewport viewport, EmitState s)
    {
        var bounds = s.Node.Bounds;
        var scroll = viewport.Scroll;
        if (s.Node.Element is { } at) s.Input.Add(new TableRegion(bounds, viewport, at));
        ReportTable(viewport);

        var view = scroll.Viewport;
        var pinnedAcross = (float)Math.Min(
            viewport.Columns.OffsetOf(Math.Clamp(viewport.PinnedColumns, 0, viewport.Columns.Count)), view.Width);
        var pinnedDown = (float)Math.Min(
            viewport.Rows.OffsetOf(Math.Clamp(viewport.PinnedRows, 0, viewport.Rows.Count)), view.Height);
        var scrollingAcross = Math.Max(0, view.Width - pinnedAcross);
        var scrollingDown = Math.Max(0, view.Height - pinnedDown);

        // The bands, in paint order: what scrolls on both axes under what is held on one, under the
        // corner held on both. Each is clipped, so none covers another.
        Span<Rect> bands =
        [
            new(bounds.X + pinnedAcross, bounds.Y + pinnedDown, scrollingAcross, scrollingDown),
            new(bounds.X, bounds.Y + pinnedDown, pinnedAcross, scrollingDown),
            new(bounds.X + pinnedAcross, bounds.Y, scrollingAcross, pinnedDown),
            new(bounds.X, bounds.Y, pinnedAcross, pinnedDown),
        ];
        var firstStop = s.Input.StopCount;
        for (var marks = 0; marks < 2; marks++)
        {
            for (var band = 0; band < bands.Length; band++)
            {
                if (bands[band].Width <= 0 || bands[band].Height <= 0) continue;
                EmitTableBand(viewport, s, bands[band], band, mark: marks == 1);
            }
        }
        // Painted band by band, read row by row: the Tab order follows the cells, not the paint.
        if (s.Node.Element is { } table) s.Input.OrderStopsFrom(firstStop, stop => CellOf(stop.Element, table));
        EmitScrollbars(scroll, s, bounds);
    }

    /// <summary>The row and the column of the cell a stop lies in: the element right under the
    /// viewport's own on the way up from the stop's.</summary>
    private static (int Row, int Column) CellOf(Element at, Element table)
    {
        for (var parent = at.Parent; parent is not null; at = parent, parent = parent.Parent)
            if (ReferenceEquals(parent, table))
                return at.Configuration is TableViewCell cell ? (cell.Row, cell.Column) : (int.MaxValue, int.MaxValue);
        return (int.MaxValue, int.MaxValue);
    }

    /// <summary>The cells of one band, or its marks, under the band's clip. A mark takes no press and
    /// no focus: it is drawn over the cells, which keep every press it covers, and a control inside one
    /// is no stop of the Tab order.</summary>
    private void EmitTableBand(TableViewport viewport, EmitState s, Rect band, int which, bool mark)
    {
        s.Builder.PushClip(new RRect(band));
        var input = mark ? s.Input.Under(new Rect(band.X, band.Y, 0, 0)).WithoutFocusStops() : s.Input.Under(band);
        var outerSurface = s.Press.Surface;
        s.Press.Surface = Intersect(outerSurface, band);
        foreach (var child in s.Node)
        {
            if (child.Source is not TableViewCell cell || cell.Mark != mark) continue;
            if (BandOf(cell, viewport) != which) continue;
            if (mark && cell.Glide is { } glide && Glided(child, glide, viewport, which, s) is { } moved)
            {
                s.Builder.PushTransform(moved);
                Emit(s with { Node = child, Input = input });
                s.Builder.Pop();
                continue;
            }
            Emit(s with { Node = child, Input = input });
        }
        s.Press.Surface = outerSurface;
        s.Builder.PopClip();
    }

    /// <summary>
    /// Where a gliding mark is drawn this frame (<see cref="TableViewCell.Glide"/>, web twin: the mark's
    /// transition in <c>lowerTableCell</c>), as the matrix that takes the rectangle it was laid out over to the
    /// one it has glided to; null where it stands over its range. Its four edges glide through the frame's
    /// transition clock in the CELLS' coordinates, the band's scroll added in and taken back out, so a mark
    /// moved to other cells glides there while a grid that scrolls carries it at once. The clock snaps under
    /// reduced motion and mounts a mark it never saw where it stands.
    /// </summary>
    private static Matrix2D? Glided(LayoutNode child, MotionSpec glide, TableViewport viewport, int band, EmitState s)
    {
        if (s.Motion.Transitions is not { } clock || child.Element is not { } at) return null;
        var offset = viewport.Scroll.Offset;
        var across = band is 0 or 2 ? (float)offset.Across : 0f;
        var down = band is 0 or 1 ? (float)offset.Down : 0f;
        var target = child.Bounds;
        var spec = new TransitionSpec(StyleChannels.All, glide.DurationMs) { Easing = glide.Curve };
        var time = s.Motion.TimeMs;
        var reduced = s.Motion.Reduced;
        var x = clock.Resolve(at, GlideChannel.TranslateX, target.X + across, time, spec, reduced) - across;
        var y = clock.Resolve(at, GlideChannel.TranslateY, target.Y + down, time, spec, reduced) - down;
        var width = clock.Resolve(at, GlideChannel.Width, target.Width, time, spec, reduced);
        var height = clock.Resolve(at, GlideChannel.Height, target.Height, time, spec, reduced);
        if (x == target.X && y == target.Y && width == target.Width && height == target.Height) return null;
        var scaleX = target.Width > 0 ? width / target.Width : 1f;
        var scaleY = target.Height > 0 ? height / target.Height : 1f;
        return Matrix2D.Translation(-target.X, -target.Y) * Matrix2D.Scale(scaleX, scaleY) * Matrix2D.Translation(x, y);
    }

    /// <summary>Which band a cell lies in: its first row and its first column say.</summary>
    private static int BandOf(TableViewCell cell, TableViewport viewport)
    {
        var pinnedRow = cell.Row < viewport.PinnedRows;
        var pinnedColumn = cell.Column < viewport.PinnedColumns;
        return pinnedRow && pinnedColumn ? 3 : pinnedRow ? 2 : pinnedColumn ? 1 : 0;
    }

    /// <summary>A track and its thumb on each axis the grid overflows, in the theme's
    /// <see cref="IAppTheme.ScrollbarTrack"/> and <see cref="IAppTheme.ScrollbarThumb"/>.</summary>
    private static void EmitScrollbars(ITableScrollModel scroll, EmitState s, Rect bounds)
    {
        var track = s.Theme.ScrollbarTrack.Resolve(s.Mode);
        var thumb = s.Theme.ScrollbarThumb.Resolve(s.Mode);
        foreach (var axis in (ReadOnlySpan<ScrollAxis>)[ScrollAxis.Vertical, ScrollAxis.Horizontal])
        {
            if (scroll.TrackOf(axis) is not { } strip || scroll.ThumbOf(axis) is not { } knob) continue;
            s.Builder.FillRRect(new RRect(Offset(strip, bounds)), Paint.Solid(track));
            var inner = Offset(knob, bounds);
            inner = new Rect(inner.X + ThumbInset, inner.Y + ThumbInset,
                Math.Max(0, inner.Width - 2 * ThumbInset), Math.Max(0, inner.Height - 2 * ThumbInset));
            var radius = Math.Min(inner.Width, inner.Height) / 2;
            s.Builder.FillRRect(new RRect(inner, new CornerRadii(radius)), Paint.Solid(thumb));
        }
    }

    private static Rect Offset(Rect local, Rect origin) =>
        new(origin.X + local.X, origin.Y + local.Y, local.Width, local.Height);

    /// <summary>Tells the viewport's owner where the grid stands and how large the window is, each
    /// only when it changed since it was last told.</summary>
    private static void ReportTable(TableViewport viewport)
    {
        var scroll = viewport.Scroll;
        var report = Reported.GetValue(scroll, _ => new TableReport());
        var view = scroll.Viewport;
        if (!report.Told || view != report.Viewport)
        {
            report.Viewport = view;
            viewport.OnViewportChanged?.Invoke(view);
        }
        if (!report.Told || scroll.Offset != report.Offset)
        {
            report.Offset = scroll.Offset;
            viewport.OnScrolled?.Invoke(scroll.Offset);
        }
        report.Told = true;
    }

    private sealed class TableReport
    {
        public bool Told;
        public TableOffset Offset;
        public Size Viewport;
    }
}
