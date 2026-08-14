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

    /// <summary>
    /// A trigger that OPENS something says so — aria-expanded on the accordion header, the
    /// select's field and the menu's trigger. A rotated chevron is paint; the attribute is the
    /// answer. And a plain button carries nothing: "controls no disclosure" and "closed" are
    /// different answers, the same null-versus-false line Selected draws.
    /// </summary>
    [Fact]
    public void ATriggerSaysWhetherItIsOpen_AndAPlainButtonSaysNothing()
    {
        // A plain Pressable lowers to a bare <button> with no role attribute, so these are found
        // by tag rather than through ControlOf.
        HtmlNode ButtonOf(VisualNode node) => Walk(Render(node)).Single(c => c.Tag == "button");
        var closed = ButtonOf(new Pressable(new Text("More", TypeRole.Label), () => { }) { Expanded = false });
        var open = ButtonOf(new Pressable(new Text("More", TypeRole.Label), () => { }) { Expanded = true });
        var plain = ButtonOf(new Pressable(new Text("Save", TypeRole.Label), () => { }));

        closed.Attributes["aria-expanded"].Should().Be("false");
        open.Attributes["aria-expanded"].Should().Be("true");
        plain.Attributes.Should().NotContainKey("aria-expanded");
    }

    /// <summary>The accordion is the live proof: its header states the section's state.</summary>
    [Fact]
    public void AnAccordionHeaderStatesItsSection()
    {
        var accordion = new Accordion([
            new AccordionItem("First") { Content = new Text("body", TypeRole.BodyM) },
            new AccordionItem("Second") { Content = new Text("body", TypeRole.BodyM) },
        ], openIndex: 0);

        var html = Render(accordion);
        var headers = Walk(html)
            .Where(node => node.Attributes.ContainsKey("aria-expanded"))
            .Select(node => node.Attributes["aria-expanded"])
            .ToList();

        headers.Should().Equal("true", "false");
    }

    /// <summary>
    /// The dialog says its NAME and, destructive, that it is an ALERT. Without the label a screen
    /// reader announces that a wall appeared but not which; without alertdialog a destructive
    /// confirm is announced politely, as if it could wait.
    /// </summary>
    [Fact]
    public void ADialogSaysItsNameAndADestructiveOneIsAnAlert()
    {
        HtmlNode LayerOf(Dialog dialog) =>
            Walk(Render(dialog)).Single(node => node.Attributes.ContainsKey("aria-modal"));

        var confirm = LayerOf(new Dialog("Delete draft?", "This cannot be undone.",
        [
            new DialogAction("Cancel", Variant: Variant.Ghost),
            new DialogAction("Delete", Variant: Variant.Destructive),
        ]));
        confirm.Attributes["role"].Should().Be("alertdialog");
        confirm.Attributes["aria-label"].Should().Be("Delete draft?");

        var plain = LayerOf(new Dialog("Rename", "Pick a new name.",
        [
            new DialogAction("Cancel", Variant: Variant.Ghost),
            new DialogAction("Save"),
        ]));
        plain.Attributes["role"].Should().Be("dialog");
    }

    /// <summary>§10 initial focus: the SAFE action carries the marker the trap prefers — Enter
    /// pressed on reflex cancels instead of destroying. Exactly one button gets it.</summary>
    [Fact]
    public void TheSafeActionCarriesTheInitialFocusMarker()
    {
        var html = Render(new Dialog("Delete draft?", "This cannot be undone.",
        [
            new DialogAction("Cancel", Variant: Variant.Ghost),
            new DialogAction("Delete", Variant: Variant.Destructive),
        ]));

        var marked = Walk(html).Where(node => node.Attributes.ContainsKey("data-eq-initial-focus")).ToList();
        marked.Should().ContainSingle();
        Walk(marked[0]).Select(node => node.TextContent).Should().Contain("Cancel");
    }

    /// <summary>
    /// A tab is the radio's mechanical twin — roving tabindex, ONE stop on the Tablist wrapper —
    /// but a tab is PICKED, not checked: aria-selected is its word, and aria-pressed never
    /// appears. The underline is paint; the attribute is the answer.
    /// </summary>
    [Fact]
    public void ATabStatesItsSelection_AndRovesUnderTheOneStop()
    {
        var html = Render(new Tabs(["Overview", "Settings"], selected: 1, onSelect: _ => { }));

        var tabs = Walk(html)
            .Where(node => node.Attributes.GetValueOrDefault("role") == "tab")
            .ToList();

        tabs.Should().HaveCount(2);
        tabs.Select(tab => tab.Attributes["aria-selected"]).Should().Equal("false", "true");
        tabs.Should().OnlyContain(tab => tab.Attributes["tabindex"] == "-1",
            "the Tablist wrapper is the one Tab stop; arrows do the walking");
        tabs.Should().OnlyContain(tab => !tab.Attributes.ContainsKey("aria-pressed"),
            "picked-ness is not pressed-ness");
    }
}
