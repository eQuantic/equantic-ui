using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A check's STATE is an attribute, never its name.
/// <para>
/// Before this, a label-less Checkbox announced "Checked" as its accessible NAME — so a screen
/// reader said the state twice when it was told the role, and worse, the name CHANGED on every
/// toggle, which reads as a different control appearing. The state now rides
/// <c>aria-checked</c>, where the role puts it, and the name is what the control is FOR.
/// </para>
/// </summary>
public class CheckStateSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode ControlOf(VisualNode node) =>
        Walk(Render(node)).Single(candidate => candidate.Attributes.ContainsKey("role"));

    [Fact]
    public void ACheckboxSaysItsStateInAriaChecked_NotInItsName()
    {
        var control = ControlOf(new Checkbox(true, () => { }, "Send me the newsletter"));

        control.Attributes["role"].Should().Be("checkbox");
        control.Attributes["aria-checked"].Should().Be("true");
        control.Attributes.Should().NotContainKey("aria-pressed", "checked-ness is not pressed-ness");
        // The name is what the control is FOR. The visible label already provides it; there is no
        // aria-label repeating the state.
        control.Attributes.GetValueOrDefault("aria-label", "").Should().NotContainAny("Checked", "Unchecked");
    }

    /// <summary>The name never CHANGES with the state — a control whose name toggles reads as a
    /// different control to assistive tech every time.</summary>
    [Fact]
    public void TheNameIsStableAcrossTheToggle()
    {
        string NameOf(bool @checked) =>
            ControlOf(new Checkbox(@checked, () => { }, "Terms")).Attributes.GetValueOrDefault("aria-label", "");

        NameOf(false).Should().Be(NameOf(true));
    }

    /// <summary>"mixed" is the third state ARIA gives a checkbox and nothing else — the "select
    /// all" over a partly-selected set. It wins over Checked, being about the whole set.</summary>
    [Fact]
    public void AnIndeterminateCheckboxIsMixed()
    {
        var control = ControlOf(new Checkbox(false, () => { }, "Select all") { Indeterminate = true });

        control.Attributes["aria-checked"].Should().Be("mixed");
    }

    [Fact]
    public void ASwitchIsASwitch_AndNeverMixed()
    {
        var control = ControlOf(new Switch(true, () => { }) { Label = "Dark mode" });

        control.Attributes["role"].Should().Be("switch");
        control.Attributes["aria-checked"].Should().Be("true");
        control.Attributes["aria-label"].Should().Be("Dark mode", "the name says what it is FOR, not its state");
    }

    /// <summary>Unlike a radio, a check keeps its own Tab stop: it is not one choice inside a
    /// composite, it IS the control. The button element is naturally focusable — what must not
    /// happen is the roving tabindex="-1" a radio gets.</summary>
    [Fact]
    public void ACheckKeepsItsOwnTabStop()
    {
        var checkbox = ControlOf(new Checkbox(false, () => { }, "Terms"));
        var toggle = ControlOf(new Switch(false, () => { }) { Label = "Dark mode" });

        checkbox.Attributes.GetValueOrDefault("tabindex").Should().NotBe("-1");
        toggle.Attributes.GetValueOrDefault("tabindex").Should().NotBe("-1");
        checkbox.Tag.Should().Be("button", "a button is focusable without any tabindex at all");
    }
}
