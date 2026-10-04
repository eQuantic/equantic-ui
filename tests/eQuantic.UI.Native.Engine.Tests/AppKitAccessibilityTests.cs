using System.Runtime.Versioning;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What AppKit itself is handed, read back from the elements through the real dispatch — the
/// question <see cref="NativeRoleTests"/> cannot ask, since the table is only what the bridge is
/// TOLD (#338).
/// <para>
/// A row in <see cref="eQuantic.UI.Native.Components.NativeRole"/> is a claim until an element
/// carries it, and a tab is where the two can part: AppKit names it with two words, a radio button
/// whose subrole is AXTabButton, and a bridge that set the role and dropped the subrole would
/// announce every tab as a radio button with the table still right. So the elements are built the
/// way <c>accessibilityChildren</c> builds them for VoiceOver, and each one is asked what it is.
/// </para>
/// </summary>
[SupportedOSPlatform("macos")]
public class AppKitAccessibilityTests
{
    [MacFact]
    public void EachPressableRoleReachesAppKitInItsOwnWords()
    {
        SemanticNode NodeOf(SemanticRole role, string name, int index) =>
            new(role, $"p{index}", new Rect(0, index * 30, 120, 24), name, null, false);
        IReadOnlyList<SemanticNode> tree =
        [
            // In the states the walk hands them: a chosen radio's check, a picked tab's selection.
            NodeOf(SemanticRole.Radio, "Express", 0) with { Checked = SemanticCheck.On },
            NodeOf(SemanticRole.Tab, "Overview", 1) with { Selected = true },
            NodeOf(SemanticRole.MenuItem, "Rename", 2),
            NodeOf(SemanticRole.Option, "Lisbon", 3),
            NodeOf(SemanticRole.Destination, "Inbox", 4),
            NodeOf(SemanticRole.Button, "Save", 5),
        ];

        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var children = PhotonAccessibility.BuildChildren(view);

            var said = new Dictionary<string, (string? Role, string? Subrole)>();
            var elements = new Dictionary<string, IntPtr>();
            var count = SendULong(children, Sel("count"));
            for (nuint index = 0; index < count; index++)
            {
                var element = Send(children, Sel("objectAtIndex:"), index);
                var name = FromNSString(Send(element, Sel("accessibilityLabel")))!;
                elements[name] = element;
                said[name] = (
                    FromNSString(Send(element, Sel("accessibilityRole"))),
                    FromNSString(Send(element, Sel("accessibilitySubrole"))));
            }

            said.Should().HaveCount(tree.Count, "every node is one element VoiceOver can land on");
            said["Express"].Should().Be(("AXRadioButton", null));
            said["Overview"].Should().Be(("AXRadioButton", "AXTabButton"),
                "a tab is a radio button until its subrole says otherwise, and the subrole is what VoiceOver reads as \"tab\"");
            said["Rename"].Should().Be(("AXMenuItem", null));
            said["Lisbon"].Should().Be(("AXMenuItem", null));
            said["Inbox"].Should().Be(("AXButton", null), "a destination is named as a button, where the user is rides AXSelected");
            said["Save"].Should().Be(("AXButton", null),
                "the control: a role with no subrole in the table gets none from the bridge either");

            // And the states the roles are read with. A radio's check is its AXValue, the 0/1 a
            // checkbox carries too, and a tab's selection is AXSelected, Core-AAM's word for both.
            SendLong(Send(elements["Express"], Sel("accessibilityValue")), Sel("longValue")).Should().Be(1,
                "a chosen radio says so through AXValue, which VoiceOver reads as its state");
            SendBool(elements["Overview"], Sel("isAccessibilitySelected")).Should().BeTrue(
                "a picked tab says so through AXSelected");
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    /// <summary>One element as AppKit answers for it.</summary>
    private readonly record struct Said(string? Role, string? Subrole, string? Description, string Label, IntPtr Element);

    /// <summary>
    /// Builds the bridge's elements for a page laid out by a real host, through the real walk, and
    /// asks each one what it is, in the order VoiceOver walks them. The DESCRIPTION is AppKit's own
    /// word for the role, which is what VoiceOver speaks, so a row's claim about it is read back
    /// rather than restated.
    /// </summary>
    private static List<Said> ReadBack(VisualNode page)
    {
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 900);
        host.RenderFrame(new DisplayListBuilder());

        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = host.Semantics;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var children = PhotonAccessibility.BuildChildren(view);
            var said = new List<Said>();
            var count = SendULong(children, Sel("count"));
            for (nuint index = 0; index < count; index++)
            {
                var element = Send(children, Sel("objectAtIndex:"), index);
                said.Add(new Said(
                    FromNSString(Send(element, Sel("accessibilityRole"))),
                    FromNSString(Send(element, Sel("accessibilitySubrole"))),
                    FromNSString(Send(element, Sel("accessibilityRoleDescription"))),
                    FromNSString(Send(element, Sel("accessibilityLabel"))) ?? "",
                    element));
            }
            return said;
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    /// <summary>
    /// The containers and the trigger #500 and #501 found lost, from REAL components: a
    /// <see cref="Tabs"/>, a <see cref="RadioGroup"/>, a <see cref="Select"/> and a destructive
    /// <see cref="Dialog"/>, laid out by a host and walked, then built into elements the way
    /// <c>accessibilityChildren</c> builds them. Before, AppKit was handed one AXSlider with no name
    /// for each of the first two, an AXButton for the field and an AXGroup with no subrole for the
    /// dialog.
    /// </summary>
    [MacFact]
    public void ATabBarARadioGroupAComboBoxAndADialogReachAppKitInItsOwnWords()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Tabs(["Overview", "Activity"], 1, _ => { }));
        page.Add(new RadioGroup(["Small", "Large"], 0, _ => { }, "Size"));
        page.Add(new Select(["Lisbon", "Porto"], 0, _ => { }));
        page.Add(new Dialog("Delete card?", "It cannot be undone.",
            [new DialogAction("Keep", null, Variant.Ghost), new DialogAction("Delete", null, Variant.Destructive)]));

        var said = ReadBack(page);

        said.Select(element => element.Role).Should().ContainInOrder(
            ["AXTabGroup", "AXRadioButton", "AXRadioButton", "AXRadioGroup", "AXRadioButton", "AXRadioButton",
             "AXPopUpButton", "AXGroup"],
            "each container comes before what it holds, in the order the page draws them");
        said.Should().NotContain(element => element.Role == "AXSlider",
            "a tab strip and a radio group are no slider, which is what both reached AppKit as");

        var bar = said.Single(element => element.Role == "AXTabGroup");
        bar.Description.Should().Be("tab group", "AXTabGroup is NSTabView's role, and AppKit's word for it");

        var tabs = said.Where(element => element.Subrole == "AXTabButton").ToList();
        tabs.Select(tab => (tab.Label, tab.Description)).Should().Equal([("Overview", "tab"), ("Activity", "tab")],
            "each tab is a stop of its own now, which AppKit calls a tab");
        SendBool(tabs[1].Element, Sel("isAccessibilitySelected")).Should().BeTrue("the picked tab says so");
        SendBool(tabs[0].Element, Sel("isAccessibilitySelected")).Should().BeFalse();

        var group = said.Single(element => element.Role == "AXRadioGroup");
        (group.Label, group.Description).Should().Be(("Size", "radio group"),
            "the group carries the name its author gave it, in AppKit's words for one");

        var radios = said.Where(element => element is { Role: "AXRadioButton", Subrole: null }).ToList();
        radios.Select(radio => (radio.Label, radio.Description)).Should().Equal(
            [("Small", "radio button"), ("Large", "radio button")]);
        SendLong(Send(radios[0].Element, Sel("accessibilityValue")), Sel("longValue")).Should().Be(1,
            "the chosen radio says so through AXValue");

        var field = said.Single(element => element.Role == "AXPopUpButton");
        (field.Label, field.Description).Should().Be(("Lisbon", "pop up button"),
            "a select-only combobox is NSPopUpButton's role on the Mac, read by the choice it shows");
        SendBool(field.Element, Sel("isAccessibilityExpanded")).Should().BeFalse("and it says its list is closed");

        var dialog = said.Single(element => element.Subrole is "AXApplicationDialog" or "AXApplicationAlertDialog");
        (dialog.Role, dialog.Subrole, dialog.Label).Should().Be(("AXGroup", "AXApplicationAlertDialog", "Delete card?"),
            "a destructive confirm is Core-AAM's alertdialog, named by its title");
        dialog.Description.Should().Be("group",
            "AppKit's own description of the pair, the measurement the Dialog row records: what says dialog is the SUBROLE");
    }

    /// <summary>A dialog that does not interrupt carries the other subrole, the one Core-AAM gives
    /// role="dialog".</summary>
    [MacFact]
    public void AModalLayerReachesAppKitAsADialog()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(new Overlay(new Text("Are you sure?", TypeRole.BodyM)) { Label = "Confirm" });

        var dialog = ReadBack(page).Single(element => element.Label == "Confirm");

        (dialog.Role, dialog.Subrole).Should().Be(("AXGroup", "AXApplicationDialog"));
    }
}
