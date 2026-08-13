using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The dense web-console surfaces: DataTable, Pagination and Breadcrumb — plus the tristate
/// Checkbox the table's "select all" needs. Written once against the vocabulary, so these assert the
/// built tree that both realizers consume.
/// </summary>
public class ConsoleComponentTests
{
    private static readonly ComponentContext Ctx = new(PhotonTheme.Instance);

    private static readonly DataColumn[] Columns =
    [
        new("Customer", GridTrack.Flex(1.6f), Sortable: true),
        new("Amount", GridTrack.Fixed(130), TextAlignment.End),
    ];

    private static DataRow Row(string key) =>
        new(key, [new Text(key), new Text("R$ 1,00")]);

    // ---- DataTable -------------------------------------------------------------------------

    [Fact]
    public void DataTable_WithoutSelection_HasNoCheckboxColumn()
    {
        var table = new DataTable(Columns, [Row("a")]).Build(Ctx).Should().BeOfType<Column>().Subject;
        var header = ((Box)table.Children[0]).Child.Should().BeOfType<Grid>().Subject;
        header.Columns.Should().HaveCount(2, "the selection track only exists when selection does");
    }

    [Fact]
    public void DataTable_WithSelection_LeadsWithACheckboxTrack()
    {
        var table = new DataTable(Columns, [Row("a")]) { Selection = [] }
            .Build(Ctx).Should().BeOfType<Column>().Subject;
        var header = ((Box)table.Children[0]).Child.Should().BeOfType<Grid>().Subject;
        header.Columns.Should().HaveCount(3);
        header.Columns[0].Kind.Should().Be(SizeKind.Fixed);
    }

    [Fact]
    public void DataTable_HeaderCheckbox_IsIndeterminateWhenOnlySomeRowsAreSelected()
    {
        var partial = HeaderCheckbox(new DataTable(Columns, [Row("a"), Row("b")]) { Selection = ["a"] });
        partial.Indeterminate.Should().BeTrue("a tick would claim both rows are selected");
        partial.Checked.Should().BeTrue("the box is filled either way — only the glyph differs");

        var all = HeaderCheckbox(new DataTable(Columns, [Row("a"), Row("b")]) { Selection = ["a", "b"] });
        all.Indeterminate.Should().BeFalse();

        var none = HeaderCheckbox(new DataTable(Columns, [Row("a")]) { Selection = [] });
        none.Indeterminate.Should().BeFalse();
        none.Checked.Should().BeFalse();

        static Checkbox HeaderCheckbox(DataTable table)
        {
            var tree = (Column)table.Build(Ctx);
            var grid = (Grid)((Box)tree.Children[0]).Child!;
            var cell = (Row)((Box)grid.Children[0]).Child!;
            return (Checkbox)((Box)cell.Children[0]).Child!;
        }
    }

    [Fact]
    public void DataTable_SortIndicatorOnlyAppearsOnTheSortedColumn()
    {
        var table = new DataTable(Columns, [Row("a")])
        {
            SortColumn = 0,
            SortDirection = SortDirection.Descending,
            OnSort = _ => { },
        };
        var grid = (Grid)((Box)((Column)table.Build(Ctx)).Children[0]).Child!;

        var sorted = Label((Box)((Pressable)grid.Children[0]).Child);
        sorted.Children.Should().HaveCount(2);
        ((Icon)sorted.Children[1]).Glyph.Name.Should().Be("chevronDown");

        Label((Box)grid.Children[1]).Children
            .Should().HaveCount(1, "an unsorted heading shows no direction");

        // cell Box -> alignment Row -> the heading's own Row
        static Row Label(Box cell) => (Row)((Row)cell.Child!).Children[0];
    }

    [Fact]
    public void DataTable_OnlySortableColumnsBecomePressable()
    {
        var grid = (Grid)((Box)((Column)new DataTable(Columns, [Row("a")]) { OnSort = _ => { } }
            .Build(Ctx)).Children[0]).Child!;
        grid.Children[0].Should().BeOfType<Pressable>();
        grid.Children[1].Should().BeOfType<Box>("Amount is not sortable — it must not look like it is");
    }

    [Fact]
    public void DataTable_WithoutASortHandler_NoHeadingIsPressable()
    {
        var grid = (Grid)((Box)((Column)new DataTable(Columns, [Row("a")]).Build(Ctx)).Children[0]).Child!;
        grid.Children.Should().AllBeOfType<Box>();
    }

    [Fact]
    public void DataTable_RowsCarryTheirKey_SoSelectionSurvivesAReorder()
    {
        var tree = (Column)new DataTable(Columns, [Row("a"), Row("b")]).Build(Ctx);
        ((Box)tree.Children[1]).Key.Should().Be("a");
        ((Box)tree.Children[2]).Key.Should().Be("b");
    }

    [Fact]
    public void DataTable_HoverIsADeclarativeDiff_NotAStateCallback()
    {
        var tree = (Column)new DataTable(Columns, [Row("a")]).Build(Ctx);
        var row = (Box)tree.Children[1];
        row.Style.Hover.Should().NotBeNull("a hover that re-rendered the table would stutter");
        row.Style.Hover!.Value.Background.Should().Be(PhotonTheme.Instance.SurfaceSubtle);
    }

    [Fact]
    public void DataTable_PendingRowsUseTheRealTracks_SoNothingShiftsWhenDataLands()
    {
        var tree = (Column)new DataTable(Columns, []) { PendingRows = 2 }.Build(Ctx);
        tree.Children.Should().HaveCount(3, "header + two skeleton rows");

        var pending = (Box)tree.Children[1];
        pending.Key.Should().Be("pending-0");
        ((Grid)pending.Child!).Columns.Should().HaveCount(2, "the same tracks the real rows use");
        pending.Style.MinHeight.Should().Be(DataTable.RowHeight);
    }

    [Fact]
    public void DataTable_EmptyStateReplacesTheBody_ButNeverTheHeader()
    {
        var tree = (Column)new DataTable(Columns, []) { Empty = new Text("Nothing yet") }.Build(Ctx);
        tree.Children.Should().HaveCount(2);
        tree.Children[1].Should().BeOfType<Text>();
    }

    [Fact]
    public void DataTable_PendingRowsWinOverTheEmptyState()
    {
        var tree = (Column)new DataTable(Columns, [])
        {
            PendingRows = 1,
            Empty = new Text("Nothing yet"),
        }.Build(Ctx);
        tree.Children.Should().NotContain(child => child is Text,
            "loading is not the same as empty — claiming empty while still fetching is a lie");
    }

    // ---- Pagination ------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 1, new[] { 1 })]
    [InlineData(7, 4, new[] { 1, 2, 3, 4, 5, 6, 7 })]
    [InlineData(8, 1, new[] { 1, 2, 8 })]
    [InlineData(70, 1, new[] { 1, 2, 70 })]
    [InlineData(70, 35, new[] { 1, 34, 35, 36, 70 })]
    [InlineData(70, 70, new[] { 1, 69, 70 })]
    public void Pagination_ElidesAroundTheCurrentPage(int count, int current, int[] expected)
    {
        Pagination.Pages(count, current).Should().Equal(expected);
    }

    [Fact]
    public void Pagination_ClampsAPageOutsideTheRange()
    {
        Pagination.Pages(10, 99).Should().Contain(10).And.Contain(1);
        Pagination.Pages(0, 1).Should().BeEmpty();
    }

    [Fact]
    public void Pagination_EndsDisableTheirStep_RatherThanRemovingIt()
    {
        var first = (Row)new Pagination(70, 1).Build(Ctx);
        Step(first, last: false).Disabled.Should().BeTrue("there is no page before the first");
        Step(first, last: true).Disabled.Should().BeFalse();

        var end = (Row)new Pagination(70, 70).Build(Ctx);
        Step(end, last: false).Disabled.Should().BeFalse();
        Step(end, last: true).Disabled.Should().BeTrue();

        static Button Step(Row row, bool last) =>
            (Button)row.Children[last ? 2 : 0];
    }

    [Fact]
    public void Pagination_MarksTheCurrentNumberSelected_AndStopsItBeingATarget()
    {
        var numbers = (Row)((Row)new Pagination(3, 2).Build(Ctx)).Children[1];
        var current = (Pressable)numbers.Children[1];
        current.Selected.Should().BeTrue();
        current.OnPressed.Should().BeNull("pressing the page you are on does nothing");
        ((Pressable)numbers.Children[0]).Selected.Should().BeFalse();
    }

    [Fact]
    public void Pagination_PutsAnEllipsisWhereItSkipped_AndItIsNotAButton()
    {
        var numbers = (Row)((Row)new Pagination(70, 35).Build(Ctx)).Children[1];
        // 1 … 34 35 36 … 70 — five numbers, two gaps.
        numbers.Children.OfType<Pressable>().Should().HaveCount(5);
        numbers.Children.OfType<Box>().Should().HaveCount(2,
            "an ellipsis stands for a run of pages, so it cannot mean one of them");
    }

    // ---- Breadcrumb ------------------------------------------------------------------------

    [Fact]
    public void Breadcrumb_LastCrumbIsNeverALink()
    {
        var row = (Row)new Breadcrumb([
            new Crumb("Workspace", "/"),
            new Crumb("Payments", "/payments"),
        ]).Build(Ctx);

        row.Children[0].Should().BeOfType<Link>();
        row.Children[1].Should().BeOfType<Icon>("the separator sits between crumbs");
        row.Children[2].Should().BeOfType<Text>("a link to where you already are is a lie");
    }

    [Fact]
    public void Breadcrumb_CrumbWithoutAnHrefStaysText()
    {
        var row = (Row)new Breadcrumb([new Crumb("Workspace"), new Crumb("Payments")]).Build(Ctx);
        row.Children[0].Should().BeOfType<Text>();
    }

    [Fact]
    public void Breadcrumb_CurrentCrumbReadsAsPrimary()
    {
        var row = (Row)new Breadcrumb([new Crumb("A", "/"), new Crumb("B")]).Build(Ctx);
        ((Text)row.Children[2]).Color.Should().Be(PhotonTheme.Instance.TextPrimary);
        var first = (Text)((Link)row.Children[0]).Child;
        first.Color.Should().Be(PhotonTheme.Instance.TextSecondary);
    }

    // ---- Checkbox tristate -----------------------------------------------------------------

    [Fact]
    public void Checkbox_Indeterminate_ShowsTheDashAndWinsOverChecked()
    {
        var row = (Row)((Pressable)new Checkbox(true) { Indeterminate = true }.Build(Ctx)).Child;
        var glyph = (Icon)((Row)((Box)row.Children[0]).Child!).Children[0];
        glyph.Glyph.Name.Should().Be("minus");
    }

    [Fact]
    public void Checkbox_Indeterminate_StatesMixedBesideTheName_NeverInsideIt()
    {
        // The state used to BE the label ("Partly selected") — a name that changed on every toggle.
        // It rides the role's own attribute now, and the name is only what the control is for.
        var press = (Pressable)new Checkbox(false) { Indeterminate = true }.Build(Ctx);
        press.Role.Should().Be(PressableRole.Checkbox);
        press.Mixed.Should().BeTrue();
        press.Label.Should().BeNull();
    }

    [Fact]
    public void Checkbox_Indeterminate_FillsLikeACheckedBox()
    {
        var row = (Row)((Pressable)new Checkbox(false) { Indeterminate = true }.Build(Ctx)).Child;
        var box = (Box)row.Children[0];
        box.Style.Background.Should().Be(PhotonTheme.Instance.Colors(Variant.Primary).Base);
        box.Style.BorderWidth.Should().Be(0);
    }
}
