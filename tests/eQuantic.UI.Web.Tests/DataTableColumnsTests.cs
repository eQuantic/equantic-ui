using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A table whose columns a person reorders and resizes (#1538), the server's half and the reports: the
/// columns drawn in the order the app keeps and the cells under them, the header a horizontal sortable of
/// its headings, a separator handle at each fixed column's edge, and what the table reports for a drop, a
/// drag of the edge and an arrow on it. The browser's half is <c>data-table-columns.spec.ts</c>, Photon's
/// <c>DataTableColumnsHostTests</c>.
/// </summary>
public class DataTableColumnsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DataColumn[] NameSizeKind() =>
    [
        new("Name", GridTrack.Flex()),
        new("Size", GridTrack.Fixed(120), TextAlignment.End),
        new("Kind", GridTrack.Fixed(100)),
    ];

    private static DataRow Row(string key, params string[] cells) =>
        new(key, cells.Select(cell => (VisualNode)new Text(cell, TypeRole.BodyM, Theme.TextPrimary)).ToArray());

    private static VisualNode Built(DataTable table) => table.Build(new ComponentContext(Theme));

    private static HtmlNode Lowered(DataTable table) => WebRealizer.Lower(table, Theme).Render();

    private static IEnumerable<VisualNode> Walk(VisualNode node)
    {
        yield return node;
        IEnumerable<VisualNode> children = node switch
        {
            FlexNode flex => flex.Children,
            Grid grid => grid.Children,
            Stack stack => stack.Children,
            Box { Child: { } only } => [only],
            SingleChildNode single => [single.Child],
            _ => [],
        };
        foreach (var child in children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IEnumerable<HtmlNode> All(HtmlNode node) =>
        node.Children.SelectMany(All).Prepend(node);

    private static string[] Texts(VisualNode node) => Walk(node).OfType<Text>().Select(text => text.Content).ToArray();

    [Fact]
    public void TheColumnsAreDrawnInTheAppsOrder_AndEachRowsCellsFollowTheirColumn()
    {
        var table = new DataTable([new("Name", GridTrack.Flex()), new("Size", GridTrack.Fixed(80))],
            [Row("a", "alpha", "1")])
        {
            ColumnOrder = [1, 0],
        };

        Texts(Built(table)).Should().Equal(["SIZE", "NAME", "1", "alpha"],
            "the header draws Size first, and the row's cells, declared Name then Size, are read through the order");
    }

    [Fact]
    public void AnOrderIsReadLeniently_SkippingWhatIsOutOfRangeOrRepeated_AndDrawingWhatItLeavesOut()
    {
        var table = new DataTable(NameSizeKind(), []) { ColumnOrder = [2, 2, 7] };

        Texts(Built(table)).Should().Equal(["KIND", "NAME", "SIZE"],
            "Kind is drawn once, 7 names no column, and Name and Size follow in declared order");
    }

    [Fact]
    public void TheHeaderIsAHorizontalSortableOfItsHeadings_WhileTheAppKeepsTheOrder_TheSelectionCellNotAmongThem()
    {
        var table = new DataTable(NameSizeKind(), [Row("a", "alpha", "1", "doc")])
        {
            Selection = new HashSet<string>(),
            OnColumnsReordered = _ => { },
        };

        var list = All(Lowered(table)).Single(node => node.Attributes.ContainsKey("data-eq-sort-id"));
        list.Attributes["data-eq-sort-id"].Should().Be("columns");
        list.Attributes["data-eq-sort-axis"].Should().Be("x");
        list.Attributes["aria-label"].Should().Be(SdkStrings.TableColumns);
        list.Attributes["data-eq-sort-say-picked-up"].Should().Be(SdkStrings.Sorting.PickedUp,
            "the keyboard's steps are said in the SDK's words");
        All(list).Where(node => node.Attributes.ContainsKey("data-eq-sort-item"))
            .Select(node => node.Attributes["data-eq-sort-item"])
            .Should().Equal(["0", "1", "2"], "each heading is an item named by its declared position, and the checkbox is none");
        All(list).Where(node => node.Attributes.ContainsKey("data-eq-sort-item"))
            .Select(node => node.Attributes["data-eq-sort-name"])
            .Should().Equal(["Name", "Size", "Kind"], "the keyboard's steps say a heading by its column");

        var fixedOrder = new DataTable(NameSizeKind(), [Row("a", "alpha", "1", "doc")]);
        All(Lowered(fixedOrder)).Should().NotContain(node => node.Attributes.ContainsKey("data-eq-sort-id"),
            "a table whose app keeps no order offers no move");
    }

    [Fact]
    public void ADropReportsTheOrderItLeaves_AndOneThatLeavesTheOrderReportsNothing()
    {
        var reported = new List<IReadOnlyList<int>>();
        var table = new DataTable(NameSizeKind(), []) { OnColumnsReordered = reported.Add };
        var list = Walk(Built(table)).OfType<Sortable>().Single();

        list.OnDropped(new SortDrop("0", "columns", "columns", 1));
        list.OnDropped(new SortDrop("1", "columns", "columns", 1));

        reported.Should().ContainSingle("the second drop puts Size back where it was")
            .Which.Should().Equal([1, 0, 2], "Name taken out and inserted after Size");

        reported.Clear();
        var reordered = new DataTable(NameSizeKind(), []) { ColumnOrder = [2, 0, 1], OnColumnsReordered = reported.Add };
        Walk(Built(reordered)).OfType<Sortable>().Single().OnDropped(new SortDrop("2", "columns", "columns", 2));
        reported.Should().ContainSingle().Which.Should().Equal([0, 1, 2],
            "the drop's index counts the drawn order without the column, Kind last");
    }

    /// <summary>The lines a built table draws where a carried column lands: the heading they lie on, and
    /// whether at its start edge.</summary>
    private static (string Heading, bool AtStart)[] DropLines(VisualNode built) =>
        Walk(built).OfType<Stack>()
            .SelectMany(stack => stack.Children.OfType<Positioned>()
                .Where(positioned => positioned.Child is Box { Style.Background: { } fill } && fill == Theme.FocusRing)
                .Select(positioned => (Texts(stack.Children[0])[0], positioned.Start == 0)))
            .ToArray();

    [Fact]
    public void ACarriedColumn_DrawsALineWhereItWouldLand_AndNoneWhereItWouldStay()
    {
        var table = new DataTable(NameSizeKind(), []) { OnColumnsReordered = _ => { } };
        var list = Walk(Built(table)).OfType<Sortable>().Single();

        list.OnHovered!(new SortDrop("0", "columns", "columns", 1));
        DropLines(Built(table)).Should().Equal([("KIND", true)], "Name lands after Size, at Kind's start edge");

        list.OnHovered!(new SortDrop("0", "columns", "columns", 2));
        DropLines(Built(table)).Should().Equal([("KIND", false)], "after the last of the others, at its end edge");

        list.OnHovered!(new SortDrop("0", "columns", "columns", 0));
        DropLines(Built(table)).Should().BeEmpty("a place that keeps the order is no move to show");

        list.OnHovered!(new SortDrop("0", "columns", "columns", 1));
        list.OnHovered!(null);
        DropLines(Built(table)).Should().BeEmpty("the carry ended");
    }

    [Fact]
    public void AFixedColumnHasASeparatorHandleAtItsEdge_AndAFluidOneHasNone()
    {
        var table = new DataTable(NameSizeKind(), []) { OnColumnResized = (_, _) => { } };

        var handles = All(Lowered(table)).Where(node => node.Attributes.GetValueOrDefault("role") == "separator").ToArray();
        handles.Select(handle => handle.Attributes["aria-label"]).Should().Equal(
            [SdkStrings.ResizeColumn("Size"), SdkStrings.ResizeColumn("Kind")], "Name's width is the layout's");

        var size = handles[0];
        size.Attributes["aria-valuenow"].Should().Be("120");
        size.Attributes["aria-valuemin"].Should().Be("64");
        size.Attributes["aria-valuemax"].Should().Be("1024");
        size.Attributes["aria-orientation"].Should().Be("vertical", "a column's edge moves sideways and is a vertical line");
        size.Attributes["tabindex"].Should().Be("0");
        var reordering = new DataTable(NameSizeKind(), []) { OnColumnResized = (_, _) => { }, OnColumnsReordered = _ => { } };
        All(Lowered(reordering)).Where(node => node.Attributes.ContainsKey("data-eq-sort-item"))
            .Should().NotContain(item => All(item).Any(node => node.Attributes.GetValueOrDefault("role") == "separator"),
                "the handle is beside the heading's item, not in it, so a press on it picks no column up");

        var edge = All(size).Single(node => node.Attributes.ContainsKey("data-eq-drag"));
        edge.Attributes["data-eq-drag"].Should().Be("x");
        edge.Attributes["data-eq-drag-rest"].Should().Be("120", "the gesture starts from the column's width");
        edge.Attributes["data-eq-drag-min"].Should().Be("64");
        edge.Attributes["data-eq-drag-max"].Should().Be("1024");
        edge.Attributes["data-eq-drag-follows"].Should().Be("0", "the app's width moves the edge, not a translation");

        All(Lowered(new DataTable(NameSizeKind(), []))).Should().NotContain(
            node => node.Attributes.GetValueOrDefault("role") == "separator", "a table that keeps no widths has no handle");
    }

    [Fact]
    public void TheHandleReportsTheWidth_ThePointersLive_AndTheKeyboardsBySteps_WithinTheColumnsBounds()
    {
        var reported = new List<(int Column, float Width)>();
        DataColumn[] columns =
        [
            new("Name", GridTrack.Flex()),
            new("Size", GridTrack.Fixed(72)),
            new("Kind", GridTrack.Fixed(1020), MaxWidth: 1024),
        ];
        var built = Built(new DataTable(columns, []) { OnColumnResized = (column, width) => reported.Add((column, width)) });
        var handles = Walk(built).OfType<Adjustable>().Where(node => node.Role == AdjustableRole.Separator).ToArray();
        var edges = Walk(built).OfType<Draggable>().ToArray();

        edges[0].OnMoved!(112);
        handles[0].OnAdjust(1);
        handles[0].OnAdjust(-1);
        handles[1].OnAdjust(1);

        reported.Should().Equal([(1, 112f), (1, 88f), (1, 64f), (2, 1024f)],
            "the pointer reports the gesture's width, an arrow steps 16 dp, and both stop at the bounds");

        reported.Clear();
        var atMinimum = Built(new DataTable([new("Size", GridTrack.Fixed(64))], []) { OnColumnResized = (c, w) => reported.Add((c, w)) });
        Walk(atMinimum).OfType<Adjustable>().Single().OnAdjust(-1);
        reported.Should().BeEmpty("a step that cannot move the edge reports nothing");
    }

    [Fact]
    public void ASeparatorIsAnnouncedWithItsValueAndOrientation_AndAnAdjustableSaysItsAxis()
    {
        HtmlNode Host(Adjustable node) => WebRealizer.Lower(node, Theme).Render();
        var value = new RangeValue(120, 64, 1024);
        var child = new Box(new BoxStyle());

        var separator = Host(new Adjustable(child, _ => { }) { Role = AdjustableRole.Separator, Value = value });
        separator.Attributes["role"].Should().Be("separator");
        separator.Attributes["aria-orientation"].Should().Be("vertical");
        separator.Attributes["aria-valuenow"].Should().Be("120");

        Host(new Adjustable(child, _ => { }) { Role = AdjustableRole.Separator, Value = value, Axis = DragAxis.Vertical })
            .Attributes["aria-orientation"].Should().Be("horizontal", "a separator moved up and down is a horizontal line");

        var valueless = Host(new Adjustable(child, _ => { }) { Role = AdjustableRole.Separator });
        valueless.Attributes["role"].Should().Be("group", "a focusable separator requires a value, as a slider does");
        valueless.Attributes.Should().NotContainKey("aria-orientation");

        Host(new Adjustable(child, _ => { }) { Value = value, Axis = DragAxis.Vertical })
            .Attributes["aria-orientation"].Should().Be("vertical");
        Host(new Adjustable(child, _ => { }) { Value = value })
            .Attributes.Should().NotContainKey("aria-orientation", "a horizontal slider is ARIA's default and writes nothing");
    }

    [Fact]
    public void AHeadingThatSorts_IsNamedInTheSdksWords()
    {
        var table = new DataTable([new("Size", GridTrack.Fixed(80), Sortable: true)], []) { OnSort = _ => { } };

        Walk(Built(table)).OfType<Pressable>().Single().Label.Should().Be(SdkStrings.SortByColumn("Size"));
    }
}
