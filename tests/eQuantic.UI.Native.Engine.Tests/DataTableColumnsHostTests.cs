using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A table's columns reordered and resized on Photon (#1538), driven through the host the way a shell drives
/// it: a press, moves, frames and a release, or keys. The table is composed of the nodes Photon already
/// carries (a horizontal sortable, a draggable edge, a separator), so each case is one the web proves in
/// <c>data-table-columns.spec.ts</c>, run here on the other target.
/// </summary>
public class DataTableColumnsHostTests
{
    /// <summary>A page that keeps the table's order and widths, as an app does.</summary>
    private sealed class Page : StatefulComponent
    {
        // Headings that sort, the press a reader reaches each one by: on Photon a sortable item takes the
        // keyboard through a control of its own, as the wiki's Sortable section says.
        public readonly DataColumn[] Columns =
        [
            new("Name", GridTrack.Flex(), Sortable: true),
            new("Size", GridTrack.Fixed(120), Sortable: true),
            new("Kind", GridTrack.Fixed(100), Sortable: true),
        ];

        public readonly List<int> Sorted = [];

        public IReadOnlyList<int>? Order;
        public readonly List<IReadOnlyList<int>> Reordered = [];
        public readonly List<(int Column, float Width)> Resized = [];

        public override VisualNode Build(ComponentContext context) =>
            new DataTable(Columns, [new DataRow("a", [new Text("alpha"), new Text("1"), new Text("doc")])])
            {
                ColumnOrder = Order,
                OnSort = Sorted.Add,
                OnColumnsReordered = order =>
                {
                    Reordered.Add(order);
                    SetState(() => Order = order);
                },
                OnColumnResized = (column, width) =>
                {
                    Resized.Add((column, width));
                    SetState(() => Columns[column] = Columns[column] with { Track = GridTrack.Fixed(width) });
                },
            };
    }

    private static (PhotonHost Host, Page Page) Open()
    {
        var page = new Page();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 600, 400);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, page);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    /// <summary>Where the last frame drew the heading of the column declared at <paramref name="index"/>.</summary>
    private static Rect HeadingAt(PhotonHost host, int index) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is SortableItem { } item && item.Id == $"{index}").Bounds;

    /// <summary>Where the last frame drew the resize handle's edge of the column named <paramref name="header"/>.</summary>
    private static Rect HandleAt(PhotonHost host, string header) =>
        Walk(host.LastFrame!.Root)
            .First(n => n.Source is Adjustable { Role: AdjustableRole.Separator } a && a.Label == SdkStrings.ResizeColumn(header))
            .Bounds;

    [Fact]
    public void APointer_DragsAHeadingPastTheNextOne_AndTheTableReportsTheOrder()
    {
        var (host, page) = Open();
        var name = HeadingAt(host, 0).Center;
        var size = HeadingAt(host, 1);
        var kind = HeadingAt(host, 2);
        // Past Size's middle and short of Kind's: between them.
        var x = (size.Center.X + kind.Center.X) / 2;

        host.PressDown(name.X, name.Y);
        host.PointerMove(name.X + 10, name.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(x, name.Y);
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.PressUp(x, name.Y);
        host.RenderFrame(new DisplayListBuilder(), 64);

        page.Reordered.Should().ContainSingle().Which.Should().Equal([1, 0, 2]);
        page.Sorted.Should().BeEmpty("a press that carried the heading does not sort");
        HeadingAt(host, 1).X.Should().BeLessThan(HeadingAt(host, 0).X, "the page kept the order and Size is drawn first");
    }

    [Fact]
    public void TheKeyboard_MovesAColumn_SayingEachStepInTheSdksWords()
    {
        var (host, page) = Open();
        // The stops in reading order: Name's heading, Size's heading and its handle, Kind's heading.
        for (var i = 0; i < 4; i++) host.KeyDown("Tab").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.TakeAnnouncements();

        host.KeyDown(" ").Should().BeTrue("Space picks Kind's heading up");
        host.KeyDown("ArrowLeft").Should().BeTrue();
        host.KeyDown(" ").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 2000);

        page.Reordered.Should().ContainSingle().Which.Should().Equal([0, 2, 1]);
        page.Sorted.Should().BeEmpty("Space is the carry's, not the heading's press");
        host.TakeAnnouncements().Select(said => said.Text).Should().Contain(
            $"Dropped Kind at position 2 of 3 in {SdkStrings.TableColumns}.",
            "the heading is named by its column, not its press, and the header row by the SDK's word for columns");
    }

    [Fact]
    public void APointer_DragsAHandle_AndTheWidthFollowsLive()
    {
        var (host, page) = Open();
        var edge = HandleAt(host, "Size").Center;

        host.PressDown(edge.X, edge.Y);
        host.PointerMove(edge.X + 20, edge.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(edge.X + 40, edge.Y);
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.PressUp(edge.X + 40, edge.Y);
        host.RenderFrame(new DisplayListBuilder(), 64);

        page.Resized.Should().NotBeEmpty().And.EndWith((1, 160f), "the width the drag started from plus its travel");
        page.Reordered.Should().BeEmpty("a press on the handle never picks the column up");
        HeadingAt(host, 1).Width.Should().BeApproximately(160, 0.5f);
    }

    [Fact]
    public void TheKeyboard_StepsAHandle_AndAReaderMeetsASplitterWithItsRange()
    {
        var (host, page) = Open();
        for (var i = 0; i < 3; i++) host.KeyDown("Tab").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown("ArrowRight").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.KeyDown("ArrowRight").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 64);

        page.Resized.Should().Equal([(1, 136f), (1, 152f)], "each arrow steps the width the page kept by 16 dp");
        host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.Splitter && node.Label == SdkStrings.ResizeColumn("Size"))
            .Which.Range.Should().Be(new RangeValue(152, 64, 1024));
    }
}
