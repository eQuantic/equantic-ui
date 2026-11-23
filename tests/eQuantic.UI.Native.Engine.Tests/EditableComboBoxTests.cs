using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A text field a listbox hangs from is an editable combobox on Photon (#1088), as the web's text control
/// under that anchor is <c>role="combobox"</c>: named by its label, holding its text as its value, saying
/// whether its list is open, and announcing the option the arrows reach as it changes, as the web's reader
/// follows the field's <c>aria-activedescendant</c>. It read as a plain text field, and moving through the
/// list said nothing until the reader walked into the panel.
/// </summary>
public class EditableComboBoxTests
{
    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);

    /// <summary>The code editor's go-to field in small: a field in a box, its list of places under it, the row
    /// the keyboard is on its active one, and marked selected.</summary>
    /// <param name="role">What the panel is to a reader.</param>
    /// <param name="open">Whether the list shows at first.</param>
    /// <param name="chosen">The option marked selected, when it is not the one the keyboard is on.</param>
    private sealed class GoTo(AnchorPanelRole role = AnchorPanelRole.Listbox, bool open = true, int? chosen = null)
        : StatefulComponent
    {
        private static readonly string[] Places = ["Alpha", "Beta", "Gamma"];
        private int _active;
        private bool _open = open;

        /// <summary>Moves the active option, or marks none with -1, as a filter that matches nothing does.</summary>
        public void Move(int to) => SetState(() => _active = to);

        public void Toggle() => SetState(() => _open = !_open);

        public override VisualNode Build(ComponentContext context)
        {
            var field = new TextEntry("al", _ => { }) { Label = "Go to", Placeholder = "Type a name" };
            var list = new Column(gap: 0);
            for (var i = 0; i < Places.Length; i++)
            {
                list.Add(new Pressable(new Text(Places[i], TypeRole.BodyM), () => { })
                {
                    Role = PressableRole.Option,
                    Selected = i == (chosen ?? _active),
                });
            }

            return new Anchored(new Box(new BoxStyle { Width = 240 }, field), list)
            {
                Open = _open,
                PanelRole = role,
                ActiveIndex = _active,
            };
        }
    }

    [Fact]
    public void AFieldAListboxHangsFrom_IsAnEditableComboBox_NamedByItsLabel_HoldingItsText()
    {
        var host = Host(new GoTo());
        host.RenderFrame(new DisplayListBuilder());

        var said = host.Semantics();
        said[0].Should().Match<SemanticNode>(field => field.Role == SemanticRole.EditableComboBox
            && field.Label == "Go to" && field.Value == "al" && field.Expanded == true,
            "the field is the combobox a reader types into, with its list open");
        said.Where(node => node.Role == SemanticRole.Option).Select(option => (option.Label, option.Selected))
            .Should().Equal([("Alpha", (bool?)true), ("Beta", (bool?)false), ("Gamma", (bool?)false)]);
    }

    [Fact]
    public void AClosedListSaysSo_AndAFieldWithNoListboxStaysATextField()
    {
        var closed = Host(new GoTo(open: false));
        closed.RenderFrame(new DisplayListBuilder());
        closed.Semantics()[0].Should().Match<SemanticNode>(field => field.Role == SemanticRole.EditableComboBox
            && field.Expanded == false);

        var menu = Host(new GoTo(AnchorPanelRole.Menu));
        menu.RenderFrame(new DisplayListBuilder());
        menu.Semantics()[0].Role.Should().Be(SemanticRole.TextField, "a menu is no list of the field's values");

        var plain = Host(new TextEntry("al", _ => { }) { Label = "Name" });
        plain.RenderFrame(new DisplayListBuilder());
        plain.Semantics().Should().ContainSingle().Which.Role.Should().Be(SemanticRole.TextField);
    }

    [Fact]
    public void TheOptionTheArrowsReach_IsAnnouncedFromTheField_AsItChanges()
    {
        var goTo = new GoTo();
        var host = Host(goTo);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.TakeAnnouncements().Should().BeEmpty("the first frame seeds what the field says, and says nothing");
        var field = host.Semantics()[0].Id;

        goTo.Move(1);
        host.RenderFrame(new DisplayListBuilder(), 2000);

        host.TakeAnnouncements().Should().ContainSingle().Which.Should().BeEquivalentTo(
            new LiveAnnouncement(field, "", "Beta", LiveRegionUrgency.Polite),
            "the option the arrows reached is said from the field that keeps the keyboard");

        goTo.Move(2);
        host.RenderFrame(new DisplayListBuilder(), 4000);
        host.TakeAnnouncements().Select(said => said.Text).Should().Equal(["Gamma"]);
    }

    [Fact]
    public void AListThatOpensAgain_SaysItsActiveOptionAgain()
    {
        var goTo = new GoTo();
        var host = Host(goTo);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.TakeAnnouncements();
        var field = host.Semantics()[0].Id;

        goTo.Toggle();
        host.RenderFrame(new DisplayListBuilder(), 2000);
        host.TakeAnnouncements().Should().BeEmpty("a list that closes says nothing");

        goTo.Toggle();
        host.RenderFrame(new DisplayListBuilder(), 4000);
        host.TakeAnnouncements().Should().ContainSingle().Which.Should().BeEquivalentTo(
            new LiveAnnouncement(field, "", "Alpha", LiveRegionUrgency.Polite),
            "the list that opens again shows its active option to a reader again, as the web sets the field's "
            + "active descendant again");
    }

    [Fact]
    public void AListWithNoActiveOption_SaysNothing_AndTheOptionTheArrowsComeBackTo_IsSaidAgain()
    {
        var goTo = new GoTo();
        var host = Host(goTo);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.TakeAnnouncements();

        goTo.Move(-1);
        host.RenderFrame(new DisplayListBuilder(), 2000);
        host.TakeAnnouncements().Should().BeEmpty("no option is active");

        goTo.Move(0);
        host.RenderFrame(new DisplayListBuilder(), 4000);
        host.TakeAnnouncements().Select(said => said.Text).Should().Equal(["Alpha"]);
    }

    [Fact]
    public void TheOptionSaid_IsTheOneTheKeyboardIsOn_NotTheOneChosen()
    {
        var goTo = new GoTo(chosen: 0);
        var host = Host(goTo);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.TakeAnnouncements();

        goTo.Move(2);
        host.RenderFrame(new DisplayListBuilder(), 2000);

        host.TakeAnnouncements().Select(said => said.Text).Should().Equal(["Gamma"],
            "the web's field names the row its active index counts to, whichever one is picked");
    }
}
