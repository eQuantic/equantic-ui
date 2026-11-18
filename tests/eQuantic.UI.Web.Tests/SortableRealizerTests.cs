using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A list a pointer sorts, the server's half (#1535): the list and its items add no element of their own,
/// they MARK their child, which the browser's controller finds; and the controller's reports are declared
/// by the server, which runs none of them, so the parity net notices the client losing one. The browser's
/// half is <c>sortable.spec.ts</c>.
/// </summary>
public class SortableRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static Column Cards(params string[] ids)
    {
        var column = new Column(gap: 0);
        foreach (var id in ids) column.Add(new SortableItem(new Text(id), id));
        return column;
    }

    [Fact]
    public void AListMarksItsChild_WithItsGroupIdAxisAndReports()
    {
        var list = Render(new Sortable(Cards("a", "b"), "todo", _ => { })
        {
            Group = "board",
            Nests = true,
            OnHovered = _ => { },
            Accepts = _ => true,
        });

        list.Tag.Should().Be("div", "the column is the list: no wrapper stands between it and its parent");
        list.Attributes["data-eq-sort"].Should().Be("board");
        list.Attributes["data-eq-sort-id"].Should().Be("todo");
        list.Attributes["data-eq-sort-axis"].Should().Be("y");
        list.Attributes["data-eq-sort-nests"].Should().Be("1");
        list.Events.Keys.Should().Contain(["eq-sort-dropped", "eq-sort-hovered", "eq-sort-accepts"]);
        list.Children.Select(item => item.Attributes["data-eq-sort-item"]).Should().Equal("a", "b");
    }

    [Fact]
    public void ANamedListIsALabelledGroup_AndAnItemWithNoFocusOfItsOwnIsATabStop()
    {
        var list = Render(new Sortable(Cards("a"), "todo", _ => { }) { Label = "To do" });

        list.Attributes["role"].Should().Be("group");
        list.Attributes["aria-label"].Should().Be("To do");
        list.Attributes["data-eq-sort-label"].Should().Be("To do");
        list.Children[0].Attributes["tabindex"].Should().Be("0", "a text is no focus of its own, and the keyboard sorts from one");
        Render(new Sortable(Cards("a"), "todo", _ => { })).Attributes.Should().NotContainKey("role");
    }

    [Fact]
    public void AnItemGivenAName_WritesItForTheKeyboardsSteps_AndOneWithoutWritesNone()
    {
        var column = new Column(gap: 0);
        column.Add(new SortableItem(new Text("Sort by Amount"), "amount") { Name = "Amount" });
        column.Add(new SortableItem(new Text("b"), "b"));
        var list = Render(new Sortable(column, "columns", _ => { }));

        list.Children[0].Attributes["data-eq-sort-name"].Should().Be("Amount",
            "the steps say the item's own name before what its subtree says");
        list.Children[1].Attributes.Should().NotContainKey("data-eq-sort-name");
    }

    [Fact]
    public void AListThatHearsNoHoverAndAsksNoGuard_DeclaresNeither_AndADisabledOneSaysSo()
    {
        var list = Render(new Sortable(Cards("a"), "todo", _ => { }) { Axis = DragAxis.Horizontal, Enabled = false });

        list.Attributes["data-eq-sort-axis"].Should().Be("x");
        list.Attributes["data-eq-sort-off"].Should().Be("1");
        list.Events.Keys.Should().Contain("eq-sort-dropped").And.NotContain(["eq-sort-hovered", "eq-sort-accepts"]);
    }

    [Fact]
    public void AListWithWords_WritesEachOnItsElementForTheController_AndOneWithNoneWritesNone()
    {
        var words = new SortAnnouncements("Use Space.", "Took {0}.", "{0} now {1}/{2} in {3}.", "{0} in {1}.",
            "Not there.", "Left {0} at {1}.", "Left {0} in {1}.", "Back.");
        var list = Render(new Sortable(Cards("a"), "todo", _ => { }, words));

        list.Attributes["data-eq-sort-say-instructions"].Should().Be("Use Space.");
        list.Attributes["data-eq-sort-say-picked-up"].Should().Be("Took {0}.");
        list.Attributes["data-eq-sort-say-moved"].Should().Be("{0} now {1}/{2} in {3}.");
        list.Attributes["data-eq-sort-say-moved-into"].Should().Be("{0} in {1}.");
        list.Attributes["data-eq-sort-say-refused"].Should().Be("Not there.");
        list.Attributes["data-eq-sort-say-dropped"].Should().Be("Left {0} at {1}.");
        list.Attributes["data-eq-sort-say-dropped-into"].Should().Be("Left {0} in {1}.");
        list.Attributes["data-eq-sort-say-put-back"].Should().Be("Back.");
        Render(new Sortable(Cards("a"), "todo", _ => { })).Attributes.Keys
            .Should().NotContain(key => key.StartsWith("data-eq-sort-say-", StringComparison.Ordinal));
    }
}
