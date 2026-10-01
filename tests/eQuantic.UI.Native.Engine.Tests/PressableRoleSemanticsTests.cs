using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every pressable role reaches the semantics tree as ITS OWN role, with its state where the web
/// puts it (#338).
/// <para>
/// The walk knew three of the nine roles and sent the rest to a catch-all, so a radio, a tab, a menu
/// item, a list option and a navigation destination all reached VoiceOver and TalkBack as buttons,
/// while the web said <c>radio</c>, <c>tab</c>, <c>menuitem</c>, <c>option</c> and
/// <c>aria-current</c> for the same components. The switch
/// has no default arm now, and this enumerates the vocabulary rather than listing it: a role
/// appended to <see cref="PressableRole"/> fails here by name until somebody says what the tree
/// calls it.
/// </para>
/// </summary>
public class PressableRoleSemanticsTests
{
    /// <summary>What the tree calls each pressable role: its own, every one.</summary>
    private static readonly Dictionary<PressableRole, SemanticRole> Expected = new()
    {
        [PressableRole.Button] = SemanticRole.Button,
        [PressableRole.Radio] = SemanticRole.Radio,
        [PressableRole.Checkbox] = SemanticRole.Checkbox,
        [PressableRole.Switch] = SemanticRole.Switch,
        [PressableRole.Tab] = SemanticRole.Tab,
        [PressableRole.MenuItem] = SemanticRole.MenuItem,
        [PressableRole.Option] = SemanticRole.Option,
        [PressableRole.Destination] = SemanticRole.Destination,
        [PressableRole.GridCell] = SemanticRole.GridCell,
    };

    /// <summary>One pressable standing alone, named so it can be found, in the state asked for.</summary>
    private static SemanticNode NodeOf(PressableRole role, bool selected)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Pressable(new Text("x", TypeRole.Label), () => { })
            { Label = "Choice", Role = role, Selected = selected });
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics().Single(node => node.Label == "Choice");
    }

    [Fact]
    public void EveryPressableRoleReachesItsOwnSemanticRole()
    {
        var roles = Enum.GetValues<PressableRole>();
        roles.Should().HaveCountGreaterThan(8, "the vocabulary really does hold that many roles");

        foreach (var role in roles)
        {
            Expected.Should().ContainKey(role,
                $"{role} has no row here, so nothing says what the semantics tree calls it");
            NodeOf(role, selected: false).Role.Should().Be(Expected[role],
                $"a {role} would otherwise reach VoiceOver and TalkBack as something it is not");
        }
    }

    /// <summary>A radio is CHECKED — the web's <c>aria-checked</c>, AppKit's AXValue, Android's
    /// <c>isChecked</c> — and never picked, which is a tab's state and reads differently.</summary>
    [Fact]
    public void ARadioCarriesItsCheckBesideItsName()
    {
        var chosen = NodeOf(PressableRole.Radio, selected: true);
        chosen.Checked.Should().Be(SemanticCheck.On);
        chosen.Selected.Should().BeNull("a radio is checked, and saying both would announce it twice");
        chosen.Label.Should().Be("Choice", "the state rides beside the name, never inside it");

        NodeOf(PressableRole.Radio, selected: false).Checked.Should().Be(SemanticCheck.Off,
            "an unchosen radio says so, the way aria-checked=\"false\" does");
    }

    /// <summary>A tab and an option are PICKED — the web's <c>aria-selected</c> — in both states, and
    /// carry no check.</summary>
    [Theory]
    [InlineData(PressableRole.Tab)]
    [InlineData(PressableRole.Option)]
    public void ATabAndAnOptionArePickedRatherThanChecked(PressableRole role)
    {
        var picked = NodeOf(role, selected: true);
        picked.Selected.Should().BeTrue();
        picked.Checked.Should().BeNull();

        NodeOf(role, selected: false).Selected.Should().BeFalse(
            "not picked is an answer too, the way aria-selected=\"false\" is");
    }

    /// <summary>A menu item is run, never picked: it carries no state at all, whatever the author set.</summary>
    [Fact]
    public void AMenuItemCarriesNoState()
    {
        var item = NodeOf(PressableRole.MenuItem, selected: true);
        item.Checked.Should().BeNull();
        item.Selected.Should().BeNull();
        item.Current.Should().BeFalse();
    }

    /// <summary>A destination says WHERE YOU ARE through <see cref="SemanticNode.Current"/>, which the
    /// bridges read as AXSelected, the Selected trait and Android's selected state — the web's
    /// <c>aria-current</c>, and neither a check nor a pick.</summary>
    [Fact]
    public void ADestinationSaysWhereYouAre()
    {
        var here = NodeOf(PressableRole.Destination, selected: true);
        here.Role.Should().Be(SemanticRole.Destination);
        here.Current.Should().BeTrue();
        here.Selected.Should().BeNull();
        here.Checked.Should().BeNull();

        NodeOf(PressableRole.Destination, selected: false).Current.Should().BeFalse();
    }
}
