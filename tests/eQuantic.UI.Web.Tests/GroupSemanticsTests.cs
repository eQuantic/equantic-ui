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
                (SemanticRole.Option, "Lisboa"), (SemanticRole.Group, "A seguir"),
                (SemanticRole.StaticText, "A seguir"), (SemanticRole.Option, "Braga"));
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

    [Fact]
    public void AGroupHostSaysGroupAndItsLabel_AndNoLabelWhenItHasNone()
    {
        var named = WebRealizer.Lower(new Group(new Text("Delivery", TypeRole.BodyM)) { Label = "Shipping" }, Theme).Render();
        named.Attributes["role"].Should().Be("group");
        named.Attributes["aria-label"].Should().Be("Shipping");

        var nameless = WebRealizer.Lower(new Group(new Text("Delivery", TypeRole.BodyM)), Theme).Render();
        nameless.Attributes["role"].Should().Be("group");
        nameless.Attributes.Should().NotContainKey("aria-label");
    }
}
