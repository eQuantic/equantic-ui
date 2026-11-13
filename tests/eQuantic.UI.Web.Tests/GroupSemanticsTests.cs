using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A labelled <see cref="Group"/> is named and then walked into (#677): one <c>role="group"</c>
/// host carrying its label, and inside a grouped Select's listbox the heading and the group's
/// options, numbered in reading order across the groups.
/// </summary>
public class GroupSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IReadOnlyList<Announcement> OpenedReading(Select select)
    {
        using var tester = new ComponentTester(Theme);
        tester.Pump(select);
        tester.Tap(SemanticRole.ComboBox, tester.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
        tester.Pump();
        return tester.Semantics;
    }

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    [Fact]
    public void AGroupedSelectReadsEachGroupsNameBeforeItsOptions()
    {
        var reading = OpenedReading(new Select(["Lisboa"], 0, _ => { }, groups: [new OptionGroup("A seguir", ["Braga"])]));

        reading.SkipWhile(stop => stop.Role != SemanticRole.Option).Select(stop => (stop.Role, stop.Name))
            .Should().Equal(
                [(SemanticRole.Option, "Lisboa"), (SemanticRole.Group, "A seguir"), (SemanticRole.Option, "Braga")],
                "the heading is the group's name, read once: as the group, never again as text");
        reading.Where(stop => stop.Role == SemanticRole.Option).Select(stop => stop.Selected)
            .Should().Equal(true, false);
    }

    [Fact]
    public void AGroupWithNoNameWrapsNothing()
    {
        var reading = OpenedReading(new Select(groups: [new OptionGroup("", ["Braga", "Faro"])]));

        reading.SkipWhile(stop => stop.Role != SemanticRole.Option).Select(stop => (stop.Role, stop.Name))
            .Should().Equal((SemanticRole.Option, "Braga"), (SemanticRole.Option, "Faro"));
    }

    /// <summary>The field shows the choice the one index names, wherever its group stands.</summary>
    [Fact]
    public void TheFieldShowsAGroupedChoice()
    {
        using var tester = new ComponentTester(Theme);
        tester.Pump(new Select(["Lisboa"], 2, groups: [new OptionGroup("Next", ["Braga", "Faro"])]));

        // A select-only combobox's content is its value on the web, as its name is on Photon.
        tester.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Value.Should().Be("Faro");
    }

    /// <summary>The options are numbered across the groups, and the highlighted one stands inside its
    /// group: <c>aria-activedescendant</c> names an option inside the labelled group.</summary>
    [Fact]
    public void TheOptionsAreNumberedAcrossTheGroups()
    {
        var select = new Select(selectedIndex: 2, onChanged: _ => { }, groups:
            [new OptionGroup("Open", ["Lisboa", "Porto"]), new OptionGroup("Next", ["Braga"])]);
        // Open through the field's own toggle: the instance keeps its state for the next lowering.
        var trigger = Walk(WebRealizer.Lower(select, Theme).Render())
            .First(node => node.Tag == "button" && node.Events.ContainsKey("click"));
        ((Action)trigger.Events["click"])();

        var nodes = Walk(WebRealizer.Lower(select, Theme).Render()).ToList();
        var listbox = nodes.Single(node => node.Attributes.GetValueOrDefault("role") == "listbox");
        var options = nodes.Where(node => node.Attributes.GetValueOrDefault("role") == "option").ToList();
        var combobox = nodes.Single(node => node.Attributes.GetValueOrDefault("role") == "combobox");

        options.Select(option => option.Attributes["id"]).Should().Equal(
            $"{listbox.Attributes["id"]}-0", $"{listbox.Attributes["id"]}-1", $"{listbox.Attributes["id"]}-2");
        combobox.Attributes["aria-activedescendant"].Should().Be(options[2].Attributes["id"]);
        var group = nodes.Single(node => node.Attributes.GetValueOrDefault("role") == "group"
            && Walk(node).Contains(options[2]));
        group.Attributes.GetValueOrDefault("aria-label").Should().Be("Next");
        Walk(group).Should().NotContain(options[0], "an option of another group stands outside it");
    }

    /// <summary>A nameless Group lowers to exactly what its child lowers to, on the parent's axis (the
    /// axis a flexible lets shrink; a column, since null reads as across).</summary>
    [Fact]
    public void ANamelessGroupLowersToItsChild_OnTheParentsAxis()
    {
        static Column In(VisualNode child)
        {
            var column = new Column(gap: 0) { Height = 200 };
            column.Add(child);
            return column;
        }
        static Flexible Child() => new(new Text("Delivery", TypeRole.BodyM));

        HtmlRenderer.RenderNode(WebRealizer.Lower(In(new Group(Child())), Theme).Render())
            .Should().Be(HtmlRenderer.RenderNode(WebRealizer.Lower(In(Child()), Theme).Render()));
    }

    /// <summary>One rule for every node that lowers to its child's element: the OUTERMOST key wins, the
    /// one among the siblings, so keyed siblings keep distinct keys whether the child has its own or
    /// not.</summary>
    [Fact]
    public void TheOutermostKeyWins_SoKeyedSiblingsStayDistinct()
    {
        var column = new Column(gap: 0);
        column.Add(new Group(new Text("A", TypeRole.BodyM) { Key = "row" }) { Key = "a" });
        column.Add(new Group(new Text("B", TypeRole.BodyM) { Key = "row" }) { Key = "b" });
        column.Add(new Group(new Text("C", TypeRole.BodyM)) { Key = "c" });

        WebRealizer.Lower(column, Theme).Children.OfType<HtmlElement>().Select(child => child.Key)
            .Should().Equal("a", "b", "c");
    }

    /// <summary>A bookmark lands on the element its node lowers to, through a wrapper that hands its
    /// child's element on; a second id on the same element would leave a link pointing at nothing, so
    /// it is refused rather than dropped.</summary>
    [Fact]
    public void ABookmarkLands_AndASecondIdOnTheSameElementIsRefused()
    {
        WebRealizer.Lower(new Group(new Text("Next", TypeRole.BodyM)) { Bookmark = "next" }, Theme).Id
            .Should().Be("next");

        var twice = () => WebRealizer.Lower(
            new Group(new Text("Next", TypeRole.BodyM) { Bookmark = "inner" }) { Bookmark = "outer" }, Theme);
        twice.Should().Throw<InvalidOperationException>().WithMessage("*'outer'*'inner'*");
    }

    /// <summary>A container's rule looks through a nameless Group as through a component: a Positioned
    /// inside one still positions in a Stack.</summary>
    [Fact]
    public void AStackSeesAPositionedThroughANamelessGroup()
    {
        static Stack With(VisualNode layer)
        {
            var stack = new Stack();
            stack.Add(new Box(new BoxStyle { Width = 100, Height = 100 }));
            stack.Add(layer);
            return stack;
        }
        static Positioned Corner() => new(new Text("x", TypeRole.BodyM), top: 0, end: 0);

        HtmlRenderer.RenderNode(WebRealizer.Lower(With(new Group(Corner())), Theme).Render())
            .Should().Be(HtmlRenderer.RenderNode(WebRealizer.Lower(With(Corner()), Theme).Render()));
    }

    /// <summary>Blank space is no name: a group labelled with it is no group, and a Select draws no
    /// heading for it.</summary>
    [Fact]
    public void ABlankLabelIsNoName()
    {
        WebRealizer.Lower(new Group(new Text("Delivery", TypeRole.BodyM)) { Label = "  " }, Theme).Render()
            .Attributes.Should().NotContainKey("role");

        var reading = OpenedReading(new Select(groups: [new OptionGroup(" ", ["Braga"])]));
        reading.SkipWhile(stop => stop.Role != SemanticRole.Option).Select(stop => (stop.Role, stop.Name))
            .Should().Equal((SemanticRole.Option, "Braga"));
    }

    /// <summary>An open Select with no options names no active one: there is no option to name.</summary>
    [Fact]
    public void AnOpenSelectWithNoOptionsNamesNoActiveOption()
    {
        var select = new Select(groups: []);
        var trigger = Walk(WebRealizer.Lower(select, Theme).Render())
            .First(node => node.Tag == "button" && node.Events.ContainsKey("click"));
        ((Action)trigger.Events["click"])();

        var combobox = Walk(WebRealizer.Lower(select, Theme).Render())
            .Single(node => node.Attributes.GetValueOrDefault("role") == "combobox");
        combobox.Attributes.Should().NotContainKey("aria-activedescendant");
    }

    /// <summary>A paragraph with a linked run is read whatever ExcludeFromSemantics says: a link hidden
    /// from a reader that a keyboard still reaches is a trap.</summary>
    [Fact]
    public void AParagraphWithALinkIsReadEvenWhenExcluded()
    {
        var plain = WebRealizer.Lower(new Text("Heading", TypeRole.Caption) { ExcludeFromSemantics = true }, Theme).Render();
        plain.Attributes["aria-hidden"].Should().Be("true");

        var linked = WebRealizer.Lower(new Text("", TypeRole.BodyM)
        {
            ExcludeFromSemantics = true,
            Spans = [new TextRun("See "), new TextRun("terms") { Destination = "/terms" }],
        }, Theme).Render();
        linked.Attributes.Should().NotContainKey("aria-hidden");
    }

    [Fact]
    public void AGroupHostSaysGroupAndItsLabel_AndIsNoHostWithoutOne()
    {
        var named = WebRealizer.Lower(new Group(new Text("Delivery", TypeRole.BodyM)) { Label = "Shipping" }, Theme).Render();
        named.Attributes["role"].Should().Be("group");
        named.Attributes["aria-label"].Should().Be("Shipping");

        var nameless = WebRealizer.Lower(new Group(new Text("Delivery", TypeRole.BodyM)), Theme).Render();
        nameless.Attributes.Should().NotContainKey("role", "a group with no name is no group: the child stands in its place");
        nameless.Tag.Should().Be("span");
    }
}
