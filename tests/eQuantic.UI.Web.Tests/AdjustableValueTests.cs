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
/// The last test is the one that matters beyond this fix: it walks the lowered tree of everything
/// that reaches a slider role — the components AND the bare node — and refuses any host that states
/// the role without the number. It began as a list of six components, which is a guard that holds
/// for six things somebody remembered; the realizer now DERIVES the role from the value, so the
/// rule holds for a tree nobody here wrote and the list is a witness rather than the mechanism.
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
    /// The announcement is the value the THUMB is drawn from, which is the clamped one. The track
    /// halves come from a fraction clamped to 0..1, so a raw value outside the range draws a thumb
    /// at the end; announcing the raw number would put aria-valuenow outside the aria-valuemax
    /// beside it — invalid on its own terms — and make the pixels and the words describe different
    /// controls.
    /// </summary>
    [Theory]
    [InlineData(99f, "10")]
    [InlineData(-5f, "0")]
    [InlineData(7f, "7")]
    public void TheAnnouncedValueIsTheOneTheThumbIsDrawnFrom(float value, string announced)
    {
        var host = Host(new Slider(value, _ => { }) { Min = 0, Max = 10 }, "slider");

        host.Attributes["aria-valuenow"].Should().Be(announced);
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
    /// A slider with no value is NOT ANNOUNCED AS ONE. ARIA pairs the role with the number, so the
    /// realizer settles both together and a value-less slider announces <c>group</c> — a focusable
    /// container the arrows adjust, which is exactly what it is. The pairing lives in the realizer
    /// rather than in the component library because the library is not the only way an Adjustable
    /// is built: these two paths were the invalid pair the components no longer produce, and
    /// <c>UI.Adjustable</c> could produce NOTHING ELSE until its signature grew the value.
    /// </summary>
    [Fact]
    public void ASliderWithNoValueAnnouncesWhatItActuallyIs()
    {
        VisualNode[] built =
        [
            new Adjustable(new Text("knob", TypeRole.Label), _ => { }) { Label = "Budget" },
            Components.UI.Adjustable(new Text("knob", TypeRole.Label), _ => { }),
        ];

        foreach (var node in built)
        {
            var host = Host(node, "group");

            host.Attributes["tabindex"].Should().Be("0", "it is still one Tab stop for the control");
            host.Attributes.Should().NotContainKey("aria-valuenow");
        }
    }

    /// <summary>
    /// The pairing's other half. A value handed to a role that has none is not emitted either —
    /// ARIA has no valuenow for a tablist or a radiogroup, so passing one through would trade one
    /// invalid host for another.
    /// </summary>
    [Fact]
    public void AValueOnARoleThatHasNoneIsNotEmitted()
    {
        var node = new Adjustable(new Text("strip", TypeRole.Label), _ => { })
        {
            Role = AdjustableRole.Tablist,
            Value = new AdjustableValue(2, 0, 5),
        };

        var host = Host(node, "tablist");

        host.Attributes.Should().NotContainKey("aria-valuenow");
        host.Attributes.Should().NotContainKey("aria-valuemin");
        host.Attributes.Should().NotContainKey("aria-valuemax");
    }

    /// <summary>
    /// THE RULE, not the instance: wherever a slider role reaches the markup, the number reaches it
    /// too. ARIA makes aria-valuenow required on role=slider, and a host that states the role
    /// without it is not a slider a reader can read — it is a control that announces its name and
    /// stops.
    /// <para>
    /// The last three cases are the PRIMITIVE, not a component — the rule has to hold for a tree
    /// nobody in this library wrote, or it only ever held for the six things somebody remembered to
    /// list here.
    /// </para>
    /// </summary>
    [Fact]
    public void NoSliderRoleReachesTheMarkupWithoutItsValue()
    {
        (string Name, VisualNode Node)[] cases =
        [
            ("slider", new Slider(0.4f, _ => { }) { Label = "Brightness" }),
            ("slider-disabled", new Slider(0.4f) { Disabled = true }),
            ("slider-stepped", new Slider(3, _ => { }) { Min = 0, Max = 10, Step = 1 }),
            ("slider-out-of-range", new Slider(99, _ => { }) { Min = 0, Max = 10 }),
            ("tabs", new Tabs(["One", "Two"], 0, _ => { })),
            ("segmented", new SegmentedControl(["Day", "Week"], 0, _ => { })),
            ("radio-group", new RadioGroup(["Monthly", "Yearly"], 0, _ => { })),
            ("bare-node", new Adjustable(new Text("knob", TypeRole.Label), _ => { })),
            ("ui-factory", Components.UI.Adjustable(new Text("knob", TypeRole.Label), _ => { })),
            ("ui-factory-valued", Components.UI.Adjustable(new Text("knob", TypeRole.Label), _ => { },
                new AdjustableValue(0.4f, 0, 1))),
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

        // And the sweep really did MEET slider roles: a guard that finds none passes by looking in
        // the wrong place, which is the shape of failure this whole body of work is about. The
        // disabled slider is the one that legitimately has none — it is not a Tab stop, so it never
        // becomes an Adjustable at all, and asserting otherwise is how this assertion first failed.
        var hosts = cases.ToDictionary(one => one.Name,
            one => Walk(Lower(one.Node)).Count(n => n.Attributes.GetValueOrDefault("role") == "slider"));

        hosts["slider"].Should().Be(1);
        hosts["slider-stepped"].Should().Be(1);
        hosts["slider-out-of-range"].Should().Be(1);
        hosts["ui-factory-valued"].Should().Be(1, "the factory can express a valid slider now");
        hosts["slider-disabled"].Should().Be(0, "a disabled slider is not a Tab stop and never wraps in an Adjustable");
        hosts["bare-node"].Should().Be(0, "a value-less node announces group — that is the rule, not an omission");
        hosts["ui-factory"].Should().Be(0, "same node, same rule, through the public factory");
    }
}
