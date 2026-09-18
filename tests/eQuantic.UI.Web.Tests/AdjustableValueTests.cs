using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An <c>Adjustable</c> says WHAT ITS VALUE IS (spec C7).
/// <para>
/// <c>role="slider"</c> requires <c>aria-valuenow</c>. Both realizers emitted the role, the tab
/// index and the name — and no value at all, which is invalid ARIA and leaves a screen-reader user
/// with what the control is FOR and never what it holds. The node carried no value to emit: it had
/// a child, a direction callback, a label and a role, which is why the fix is a vocabulary change
/// rather than a line in the Slider.
/// </para>
/// <para>
/// The last test is the one that matters beyond this fix: it walks the lowered tree of every
/// component that reaches a slider role and refuses any host that states the role without the
/// number. A second control that grows a slider role cannot repeat this.
/// </para>
/// </summary>
public class AdjustableValueTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Lower(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static HtmlNode Host(VisualNode node, string role) =>
        Walk(Lower(node)).First(n => n.Attributes.GetValueOrDefault("role") == role);

    [Fact]
    public void ASliderStatesItsValueAndTheRangeItMovesOver()
    {
        var host = Host(new Slider(0.4f, _ => { }) { Label = "Brightness" }, "slider");

        host.Attributes["aria-valuenow"].Should().Be("0.4");
        host.Attributes["aria-valuemin"].Should().Be("0");
        host.Attributes["aria-valuemax"].Should().Be("1");
        host.Attributes["aria-label"].Should().Be("Brightness");
        host.Attributes.Should().NotContainKey("aria-valuetext",
            "a bare ratio speaks for itself; valuetext REPLACES the number, so echoing it would say the same thing twice");
    }

    /// <summary>The bounds are the caller's, not ARIA's 0–100 default — which is the reason they
    /// travel with the value rather than being assumed at the far end.</summary>
    [Fact]
    public void TheRangeIsTheOneTheCallerSet()
    {
        var host = Host(new Slider(400, _ => { }) { Min = 100, Max = 900 }, "slider");

        host.Attributes["aria-valuenow"].Should().Be("400");
        host.Attributes["aria-valuemin"].Should().Be("100");
        host.Attributes["aria-valuemax"].Should().Be("900");
    }

    /// <summary>
    /// "announces 'Limit, R$ 400'" — the words are the app's, because only the app knows whether
    /// 0.4 is a ratio, a currency or the fourth of six named steps.
    /// </summary>
    [Fact]
    public void WordsReplaceTheNumberWhenTheCallerGivesThem()
    {
        var host = Host(
            new Slider(400, _ => { }) { Min = 100, Max = 900, Label = "Limit", ValueText = "R$ 400" },
            "slider");

        host.Attributes["aria-valuetext"].Should().Be("R$ 400");
        host.Attributes["aria-valuenow"].Should().Be("400", "the number stays for anything that computes with it");
    }

    /// <summary>
    /// A tablist and a radiogroup announce a SELECTION — their items carry aria-selected and
    /// aria-checked — so a value on the group would be a second answer to a question the children
    /// already answer, and ARIA has no valuenow for either role.
    /// </summary>
    [Theory]
    [InlineData("tablist")]
    [InlineData("radiogroup")]
    public void AGroupThatPicksRatherThanMeasuresCarriesNoValue(string role)
    {
        VisualNode component = role == "tablist"
            ? new Tabs(["Overview", "Activity"], 0, _ => { })
            : new RadioGroup(["Monthly", "Yearly"], 0, _ => { });

        var host = Host(component, role);

        host.Attributes.Should().NotContainKey("aria-valuenow");
        host.Attributes.Should().NotContainKey("aria-valuemin");
        host.Attributes.Should().NotContainKey("aria-valuemax");
    }

    /// <summary>
    /// THE RULE, not the instance: wherever a slider role reaches the markup, the number reaches it
    /// too. ARIA makes aria-valuenow required on role=slider, and a host that states the role
    /// without it is not a slider a reader can read — it is a control that announces its name and
    /// stops.
    /// </summary>
    [Fact]
    public void NoSliderRoleReachesTheMarkupWithoutItsValue()
    {
        (string Name, VisualNode Node)[] cases =
        [
            ("slider", new Slider(0.4f, _ => { }) { Label = "Brightness" }),
            ("slider-disabled", new Slider(0.4f) { Disabled = true }),
            ("slider-stepped", new Slider(3, _ => { }) { Min = 0, Max = 10, Step = 1 }),
            ("tabs", new Tabs(["One", "Two"], 0, _ => { })),
            ("segmented", new SegmentedControl(["Day", "Week"], 0, _ => { })),
            ("radio-group", new RadioGroup(["Monthly", "Yearly"], 0, _ => { })),
        ];

        var naked = cases
            .SelectMany(one => Walk(Lower(one.Node)).Select(node => (one.Name, Node: node)))
            .Where(found => found.Node.Attributes.GetValueOrDefault("role") == "slider"
                && !found.Node.Attributes.ContainsKey("aria-valuenow"))
            .Select(found => found.Name)
            .ToArray();

        string.Join(", ", naked).Should().BeEmpty(
            "role=slider REQUIRES aria-valuenow; a host that states the role and not the number announces "
            + "what the control is for and never what it holds");
    }
}
