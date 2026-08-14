using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The NATIVE half of "state is an attribute, not a name": the semantics tree carries a check's
/// state beside its label, exactly the way the web's <c>aria-checked</c> does. Before this the
/// tree had no checked bit at all — the fence the interaction audit named — and a Checkbox's
/// label WAS its state.
/// </summary>
public class CheckSemanticsTests
{
    private static IReadOnlyList<SemanticNode> SemanticsOf(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    [Fact]
    public void ACheckboxCarriesItsStateBesideItsName()
    {
        var nodes = SemanticsOf(new Checkbox(true, () => { }, "Send me the newsletter"));

        var checkbox = nodes.Single(node => node.Role == SemanticRole.Checkbox);
        checkbox.Checked.Should().Be(SemanticCheck.On);
        checkbox.Label.Should().Be("Send me the newsletter",
            "the name is what the control is FOR — the state is not in it");
    }

    /// <summary>The name never changes when the state does. Cross-pinned with the web test of the
    /// same claim: a control whose name toggles reads as a different control.</summary>
    [Fact]
    public void TheNameIsStableAcrossTheToggle()
    {
        string NameOf(bool @checked) =>
            SemanticsOf(new Checkbox(@checked, () => { }, "Terms"))
                .Single(node => node.Role == SemanticRole.Checkbox).Label;

        NameOf(false).Should().Be(NameOf(true));
    }

    [Fact]
    public void AnIndeterminateCheckboxIsMixed()
    {
        var nodes = SemanticsOf(new Checkbox(false, () => { }, "Select all") { Indeterminate = true });

        nodes.Single(node => node.Role == SemanticRole.Checkbox)
            .Checked.Should().Be(SemanticCheck.Mixed);
    }

    [Fact]
    public void ASwitchIsItsOwnRole()
    {
        var nodes = SemanticsOf(new Switch(true, () => { }) { Label = "Dark mode" });

        var toggle = nodes.Single(node => node.Role == SemanticRole.Switch);
        toggle.Checked.Should().Be(SemanticCheck.On);
        toggle.Label.Should().Be("Dark mode");
    }

    /// <summary>A plain button carries NO check at all — null, not Off: "not a check" and
    /// "unchecked" are different answers, the same distinction Pressable.Selected draws.</summary>
    [Fact]
    public void APlainButtonCarriesNoCheck()
    {
        var nodes = SemanticsOf(new Button("Save", Variant.Primary));

        nodes.Single(node => node.Role == SemanticRole.Button).Checked.Should().BeNull();
    }

    /// <summary>Disclosure crosses the same way the check does: the accordion's open header says
    /// expanded, the closed one says collapsed, and a plain button says NOTHING — "controls no
    /// disclosure" and "closed" are different answers.</summary>
    [Fact]
    public void AnAccordionHeaderCarriesItsDisclosure_AndAPlainButtonNone()
    {
        var accordion = new Accordion([
            new AccordionItem("First") { Content = new Text("body", TypeRole.BodyM) },
            new AccordionItem("Second") { Content = new Text("body", TypeRole.BodyM) },
        ], openIndex: 0);

        var nodes = SemanticsOf(accordion);
        var headers = nodes.Where(node => node.Expanded is not null).ToList();

        headers.Should().HaveCount(2);
        headers[0].Expanded.Should().BeTrue();
        headers[1].Expanded.Should().BeFalse();

        SemanticsOf(new Button("Save", Variant.Primary))
            .Single(node => node.Role == SemanticRole.Button).Expanded.Should().BeNull();
    }
}
