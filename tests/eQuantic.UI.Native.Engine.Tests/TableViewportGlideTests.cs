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
