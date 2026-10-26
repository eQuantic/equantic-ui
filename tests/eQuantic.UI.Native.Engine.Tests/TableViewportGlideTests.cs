using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A keyed mark glides on Photon (#984, <see cref="TableViewCell.Glide"/>): moved to another range by a later
/// build, it is drawn on its way there over the glide's duration; a mark the previous build did not hold and a
/// mark under reduced motion stand where they are at once; and a grid that scrolls carries a mark at once.
/// </summary>
public class TableViewportGlideTests
{
    private static readonly Color Ink = Color.FromRgb(0xC0, 0x10, 0x20);
    private static readonly Color Handle = Color.FromRgb(0x10, 0x80, 0x20);

    /// <summary>Linear, so halfway through the glide the mark stands halfway.</summary>
    private static readonly MotionSpec Glide = new(200, new Curve(0f, 0f, 1f, 1f));

    private static TableViewCell Cursor(int row, int column, MotionSpec? glide) =>
        new(row, column, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            BorderColor = new ColorToken(Ink, Ink),
            BorderWidth = 2,
        }), mark: true)
        {
            Key = "cursor",
            Glide = glide,
        };

    private static (PhotonHost Host, List<TableViewCell> Marks, TableScroll Scroll) Open(bool reducedMotion,
        TableViewCell? first)
    {
        var marks = new List<TableViewCell>();
        if (first is not null) marks.Add(first);
        var scroll = new TableScroll();
        var table = new TableView(new UniformTableSpans(200, 28), new UniformTableSpans(40, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Marks = marks,
            Scroll = scroll,
        };
        var host = new PhotonHost(table, PhotonTheme.Instance, ThemeMode.Light, 900, 600)
        {
            SmoothScroll = false,
            ReducedMotion = reducedMotion,
        };
        for (var t = 0; t < 4; t++) host.RenderFrame(new DisplayListBuilder(), t * 16);
        return (host, marks, scroll);
    }

    /// <summary>Where the mark's outline is drawn across, on the frame at <paramref name="timeMs"/>.</summary>
    private static float DrawnX(PhotonHost host, float timeMs)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        var stroke = builder.Build().Commands.ToArray().Single(command => command.Paint.Color == Ink);
        return stroke.Transform.Transform(new Point(stroke.Shape.Rect.X, stroke.Shape.Rect.Y)).X;
    }

    /// <summary>A range from row 2 and column 2 across <paramref name="columns"/> columns: an outline with a
    /// six-point handle inside its corner.</summary>
    private static TableViewCell Range(int columns, MotionSpec? glide) =>
        new(2, 2, new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            BorderColor = new ColorToken(Ink, Ink),
            BorderWidth = 2,
        }, new Box(new BoxStyle { Width = 6, Height = 6, Background = new ColorToken(Handle, Handle) })), mark: true)
        {
            Key = "cursor",
            Glide = glide,
            Columns = columns,
        };

    /// <summary>The rectangle the one command painted in <paramref name="color"/> covers on the screen on the
    /// frame at <paramref name="timeMs"/>, and how thick its stroke is drawn there.</summary>
    private static (Rect Rect, float Thickness) Drawn(PhotonHost host, float timeMs, Color color)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        var command = builder.Build().Commands.ToArray().Single(c => c.Paint.Color == color);
        var shape = command.Shape.Rect;
        var from = command.Transform.Transform(new Point(shape.X, shape.Y));
        var to = command.Transform.Transform(new Point(shape.X + shape.Width, shape.Y + shape.Height));
        var stroke = command.Transform.Transform(new Point(shape.X + command.StrokeWidth, shape.Y));
        return (new Rect(from.X, from.Y, to.X - from.X, to.Y - from.Y), stroke.X - from.X);
    }

    /// <summary>The table's column <paramref name="column"/>'s left edge, the grid unscrolled: the first
    /// column is pinned, 96 wide.</summary>
    private static float LeftOf(int column) => column * 96f;

    [Fact]
    public void AMarkMovedACell_GlidesThere()
    {
        var (host, marks, _) = Open(reducedMotion: false, Cursor(2, 2, Glide));
        DrawnX(host, 100).Should().BeApproximately(LeftOf(2), 1);

        marks[0] = Cursor(2, 3, Glide);
        host.Invalidate();

        DrawnX(host, 200).Should().BeApproximately(LeftOf(2), 1, "the glide begins where the mark stood");
        DrawnX(host, 300).Should().BeApproximately((LeftOf(2) + LeftOf(3)) / 2, 2, "halfway through, halfway there");
        DrawnX(host, 400).Should().BeApproximately(LeftOf(3), 1);
        host.RenderFrame(new DisplayListBuilder(), 420);
        DrawnX(host, 440).Should().BeApproximately(LeftOf(3), 1, "and it stays");
    }

    [Fact]
    public void UnderReducedMotion_AMarkJumps()
    {
        var (host, marks, _) = Open(reducedMotion: true, Cursor(2, 2, Glide));
        DrawnX(host, 100);

        marks[0] = Cursor(2, 3, Glide);
        host.Invalidate();

        DrawnX(host, 200).Should().BeApproximately(LeftOf(3), 1, "the first frame stands it over its new cell");
    }

    [Fact]
    public void ANewMark_StandsWhereItIsAtOnce()
    {
        var (host, marks, _) = Open(reducedMotion: false, first: null);

        marks.Add(Cursor(2, 3, Glide));
        host.Invalidate();

        DrawnX(host, 200).Should().BeApproximately(LeftOf(3), 1);
    }

    [Fact]
    public void AMarkWithNoGlide_Jumps()
    {
        var (host, marks, _) = Open(reducedMotion: false, Cursor(2, 2, glide: null));
        DrawnX(host, 100);

        marks[0] = Cursor(2, 3, glide: null);
        host.Invalidate();

        DrawnX(host, 200).Should().BeApproximately(LeftOf(3), 1);
    }

    [Fact]
    public void AGlideTurnedOffAndOnAgain_StartsWhereTheMarkStands()
    {
        // Review of #1102: frames with no glide left the mark's tracks where the last glide ended, so turned on
        // again it glided back from that old rectangle.
        var (host, marks, _) = Open(reducedMotion: false, Cursor(2, 2, Glide));
        DrawnX(host, 100);
        marks[0] = Cursor(2, 3, Glide);
        host.Invalidate();
        DrawnX(host, 200);
        DrawnX(host, 450).Should().BeApproximately(LeftOf(3), 1);

        marks[0] = Cursor(2, 5, glide: null);
        host.Invalidate();
        DrawnX(host, 500).Should().BeApproximately(LeftOf(5), 1, "with no glide it jumps");

        marks[0] = Cursor(2, 5, Glide);
        host.Invalidate();
        DrawnX(host, 600).Should().BeApproximately(LeftOf(5), 1, "turned on again, it starts where it stands");
        DrawnX(host, 700).Should().BeApproximately(LeftOf(5), 1);
    }

    [Fact]
    public void AMarkThatGrows_IsLaidOutAtEachSizeOnTheWay_NeverScaled()
    {
        // Review of #1102: the glide drew the mark laid out over its new range and scaled it back to the old
        // one, so on the way from one column to three its 2-point outline was two thirds of a point and the
        // handle inside it a third of its size. Flutter's AnimatedPositioned lays its child out at each size.
        var (host, marks, _) = Open(reducedMotion: false, Range(columns: 1, Glide));
        Drawn(host, 100, Ink).Rect.Width.Should().BeApproximately(96, 3);

        marks[0] = Range(columns: 3, Glide);
        host.Invalidate();
        Drawn(host, 200, Ink);

        var outline = Drawn(host, 300, Ink);
        outline.Rect.Width.Should().BeApproximately((96 + 3 * 96) / 2f, 3, "halfway through, halfway as wide");
        outline.Thickness.Should().BeApproximately(2, 0.01f, "an outline keeps its thickness on the way");
        var handle = Drawn(host, 300, Handle);
        handle.Rect.Width.Should().BeApproximately(6, 0.01f, "what the mark holds keeps its own size");
        handle.Rect.Height.Should().BeApproximately(6, 0.01f);
        Drawn(host, 400, Ink).Rect.Width.Should().BeApproximately(3 * 96, 3);
    }

    [Fact]
    public void AGridThatScrolls_CarriesAGlidingMarkAtOnce()
    {
        var (host, _, scroll) = Open(reducedMotion: false, Cursor(2, 4, Glide));
        DrawnX(host, 100).Should().BeApproximately(LeftOf(4), 1);

        host.ScrollBy(450, 300, 96, 0, stepped: false);
        host.RenderFrame(new DisplayListBuilder(), 116);
        scroll.Offset.Across.Should().Be(96);

        DrawnX(host, 132).Should().BeApproximately(LeftOf(3), 1, "the grid moved a column, and the mark with it at once");
    }
}
