using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A combobox's trigger and a dialog reach the native tree as what they are (#501), where they
/// reached it as a button that happened to expand and as a plain group with a name. The web says
/// <c>combobox</c> for the pressable a listbox panel hangs from, and <c>dialog</c> or
/// <c>alertdialog</c> for an open modal layer, and so does this tree now.
/// <para>
/// The other half is what must NOT become a dialog. Photon opens every anchored panel in a layer of
/// its own, which the web does not have, and that layer was modal by default: it read as an unnamed
/// group in front of every open menu and select, and would have read as a dialog the moment a modal
/// layer became one. The layer is a dialog only when its panel is one, the date picker's calendar,
/// as the web's panel is.
/// </para>
/// </summary>
public class ComboBoxAndDialogSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static PhotonHost Render(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    /// <summary>Presses the one control that discloses something, then lays the next frame out.</summary>
    private static void Open(PhotonHost host)
    {
        var trigger = host.Semantics().Single(node => node.Expanded is not null);
        host.ActivatePath(trigger.Path).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 16);
    }

    private static readonly SemanticRole[] Dialogs = [SemanticRole.Dialog, SemanticRole.AlertDialog];

    [Fact]
    public void ASelectsFieldIsAComboBoxThatSaysWhetherItsListIsOpen()
    {
        var host = Render(new Select(["Lisbon", "Porto"], 0, _ => { }));

        var closed = host.Semantics().Should().ContainSingle().Subject;
        closed.Role.Should().Be(SemanticRole.ComboBox, "the field a listbox hangs from is the combobox, the web's own rule");
        closed.Label.Should().Be("Lisbon", "it is read by the choice it shows");
        closed.Expanded.Should().BeFalse();
        closed.Checked.Should().BeNull("a combobox carries no check");
        closed.Selected.Should().BeNull("and no pick of its own; its options carry those");

        Open(host);

        host.Semantics().Select(node => (node.Role, node.Label)).Should().Equal(
            [(SemanticRole.ComboBox, "Lisbon"), (SemanticRole.Button, "Dismiss"),
             (SemanticRole.Option, "Lisbon"), (SemanticRole.Option, "Porto")],
            "the open list follows the field with nothing announced between them but the scrim the "
            + "web emits too, and in particular no layer: a listbox is no dialog to anyone");
        host.Semantics()[0].Expanded.Should().BeTrue();
        host.Semantics().Where(node => node.Role == SemanticRole.Option).Select(option => option.Selected)
            .Should().Equal([true, false]);
    }

    [Fact]
    public void ATimePickersTriggerIsAComboBoxToo()
    {
        var host = Render(new TimePicker(new TimeOnly(9, 30), _ => { }, label: "Arrival"));

        host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.ComboBox)
            .Which.Should().Match<SemanticNode>(field => field.Label == "Arrival" && field.Expanded == false);
        host.Semantics().Should().NotContain(node => node.Role == SemanticRole.Button);
    }

    /// <summary>A MENU's trigger is a button on the web, one that says it has a popup, and so it
    /// stays here: only a LISTBOX makes its anchor the combobox. Its open menu is no dialog either.</summary>
    [Fact]
    public void AMenusTriggerStaysAButtonThatExpands()
    {
        var host = Render(new Menu(new Button("More"), [new MenuItem("Rename"), new MenuItem("Delete")], _ => { }));

        host.Semantics().Should().ContainSingle()
            .Which.Should().Match<SemanticNode>(trigger => trigger.Role == SemanticRole.Button
                && trigger.Label == "More" && trigger.Expanded == false);

        Open(host);

        host.Semantics().Select(node => node.Role).Should().Equal(
            [SemanticRole.Button, SemanticRole.Button, SemanticRole.MenuItem, SemanticRole.MenuItem],
            "the trigger, the scrim and the two items, and no layer in front of them: the unnamed "
            + "group the menu's layer used to announce was nothing the web says");
    }

    /// <summary>The one anchored panel that IS a dialog on the web: the calendar a date picker opens,
    /// whose focus moves inside it. Its opener stays a button, as the web's
    /// <c>aria-haspopup="dialog"</c> button is.</summary>
    [Fact]
    public void ADatePickersCalendarIsADialog()
    {
        var host = Render(new DatePicker(new DateOnly(2026, 7, 17), _ => { }, label: "Departure"));
        host.Semantics().Should().NotContain(node => Dialogs.Contains(node.Role));

        Open(host);

        var semantics = host.Semantics();
        semantics.Should().ContainSingle(node => node.Role == SemanticRole.Dialog,
            "the panel the focus moves into is a dialog, on the web and here");
        semantics.Single(node => node.Expanded is not null).Role.Should().Be(SemanticRole.Button,
            "a dialog's opener is a button, not a combobox");
        semantics.SkipWhile(node => node.Role != SemanticRole.Dialog).Skip(1)
            .Should().Contain(node => node.Role == SemanticRole.GridCell,
                "and what it holds, the calendar's days, follows it");
    }

    [Fact]
    public void AModalLayerIsADialogNamedByItsLabel()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(new Overlay(new Text("Are you sure?", TypeRole.BodyM)) { Label = "Confirm" });

        var semantics = Render(page).Semantics();

        semantics.Select(node => (node.Role, node.Label)).Should().Equal(
            [(SemanticRole.StaticText, "Cards"), (SemanticRole.Dialog, "Confirm"),
             (SemanticRole.StaticText, "Are you sure?")],
            "the page, then the dialog by its name, then what the dialog holds: role=\"dialog\" with "
            + "aria-label on the web, and a plain group with that name here until #501");
    }

    /// <summary>The Dialog component is a modal layer named by its title, and a destructive confirm
    /// INTERRUPTS: the web's <c>alertdialog</c>, which has its own words on AppKit and Android.</summary>
    [Theory]
    [InlineData(Variant.Destructive, SemanticRole.AlertDialog)]
    [InlineData(Variant.Primary, SemanticRole.Dialog)]
    public void ADialogIsADialogAndADestructiveConfirmAnAlertDialog(Variant confirm, SemanticRole expected)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Dialog("Delete card?", "It cannot be undone.",
            [new DialogAction("Keep", null, Variant.Ghost), new DialogAction("Delete", null, confirm)]));

        var semantics = Render(page).Semantics();

        semantics.Should().ContainSingle(node => Dialogs.Contains(node.Role))
            .Which.Should().Match<SemanticNode>(dialog => dialog.Role == expected && dialog.Label == "Delete card?");
        semantics.SkipWhile(node => !Dialogs.Contains(node.Role)).Skip(1).Select(node => node.Label)
            .Should().Contain(["Delete card?", "It cannot be undone.", "Keep", "Delete"],
                "the dialog is walked into, its title, body and actions after it");
    }

    /// <summary>A toast's layer is NOT modal, on either target: it is no dialog, and its live
    /// region is what speaks for it.</summary>
    [Fact]
    public void AToastIsNoDialog()
    {
        var semantics = Render(new Toast("Card removed", Variant.Info, "Undo", () => { })).Semantics();

        semantics.Should().NotContain(node => Dialogs.Contains(node.Role));
        semantics.Should().Contain(node => node.Role == SemanticRole.Group && node.Live != null);
    }

    /// <summary>
    /// Only the pressable the anchor IS becomes the combobox, the web's rule too: the role rides the
    /// anchor's ROOT. A component written as the anchor reaches the pressable it builds, the way the
    /// web reaches the element it lowers to, and so does a wrapper that holds only the pressable; a
    /// pressable INSIDE an anchor that is a row of two stays a button; and an anchor whose panel is a
    /// menu is a button that expands.
    /// </summary>
    [Fact]
    public void OnlyThePressableAnAnchorIsBecomesTheComboBox()
    {
        var list = new Text("the list", TypeRole.BodyM);

        var asComponent = Render(new Anchored(new Button("Pick"), list) { PanelRole = AnchorPanelRole.Listbox });
        asComponent.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.ComboBox)
            .Which.Label.Should().Be("Pick");

        var sized = new Box(new BoxStyle { Width = 200 },
            new Pressable(new Text("Size", TypeRole.Label), () => { }) { Expanded = false });
        var wrapped = Render(new Anchored(sized, list) { PanelRole = AnchorPanelRole.Listbox });
        wrapped.Semantics().Should().ContainSingle()
            .Which.Should().Match<SemanticNode>(field => field.Role == SemanticRole.ComboBox && field.Label == "Size",
                "a box that only sizes the trigger is not what a reader meets in its place");

        var row = new Row(gap: Space.S2);
        row.Add(new Pressable(new Text("A", TypeRole.Label), () => { }));
        row.Add(new Pressable(new Text("B", TypeRole.Label), () => { }));
        var inside = Render(new Anchored(row, list) { PanelRole = AnchorPanelRole.Listbox });
        inside.Semantics().Select(node => node.Role).Should().Equal([SemanticRole.Button, SemanticRole.Button]);

        var menu = Render(new Anchored(new Pressable(new Text("Menu", TypeRole.Label), () => { }) { Expanded = false },
            list) { PanelRole = AnchorPanelRole.Menu });
        menu.Semantics().Should().ContainSingle().Which.Role.Should().Be(SemanticRole.Button);
    }
}
