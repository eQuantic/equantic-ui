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
            new(role, index + 1, new Rect(0, index * 30, 120, 24), name, null, false);
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
        (field.Label, field.Description).Should().Be(("", "pop up button"),
            "a select-only combobox is NSPopUpButton's role on the Mac, and a Select has no label to name it (#1328)");
        FromNSString(Send(field.Element, Sel("accessibilityValue"))).Should().Be("Lisbon",
            "the choice it shows is its AXValue, as it is the web's value");
        SendBool(field.Element, Sel("isAccessibilityExpanded")).Should().BeFalse("and it says its list is closed");

        var dialog = said.Single(element => element.Subrole is "AXApplicationDialog" or "AXApplicationAlertDialog");
        (dialog.Role, dialog.Subrole, dialog.Label).Should().Be(("AXGroup", "AXApplicationAlertDialog", "Delete card?"),
            "a destructive confirm is Core-AAM's alertdialog, named by its title");
        dialog.Description.Should().Be("group",
            "AppKit's own description of the pair, the measurement the Dialog row records: what says dialog is the SUBROLE");
    }

    /// <summary>A labelled <see cref="Group"/> (#677) reaches AppKit as an AXGroup named by its label,
    /// before what it holds — the stop a grouped Select's heading is announced as.</summary>
    [MacFact]
    public void ALabelledGroupReachesAppKitAsANamedGroup()
    {
        var column = new Column(gap: Space.S2);
        column.Add(new Text("Braga", TypeRole.BodyM));
        var said = ReadBack(new Group(column) { Label = "A seguir" });

        var group = said.First();
        (group.Role, group.Label).Should().Be(("AXGroup", "A seguir"));
    }

    /// <summary>A dialog that does not interrupt carries the other subrole, the one Core-AAM gives
    /// role="dialog".</summary>
    /// <summary>A rectangle's location and length, as AppKit answers for a range (an index range).</summary>
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern eQuantic.UI.Native.Shell.Apple.NSRange SendRange(IntPtr receiver, IntPtr selector);

    /// <summary>
    /// A grid, its headers and its cells, from a real <see cref="TableView"/> (#1081): AXTable with the
    /// grid's whole count of rows and columns, and AXCell with its row and column index ranges, read back
    /// from the elements AppKit is handed.
    /// </summary>
    [MacFact]
    public void AGridAndItsCellsReachAppKitAsATableOfCells()
    {
        var table = new TableView(new UniformTableSpans(40, 28), new UniformTableSpans(10, 96),
            (row, column) => new Text($"{row}:{column}", TypeRole.BodyM))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Label = "Ledger",
            Role = TableRole.Grid,
            CellRole = (row, column) => row == 0 ? TableCellRole.ColumnHeader
                : column == 0 ? TableCellRole.RowHeader
                : TableCellRole.Cell,
        };

        var said = ReadBack(table);

        var grid = said.Single(element => element.Label == "Ledger");
        grid.Role.Should().Be("AXTable");
        SendULong(grid.Element, Sel("accessibilityRowCount")).Should().Be(40);
        SendULong(grid.Element, Sel("accessibilityColumnCount")).Should().Be(10);
        var cell = said.Single(element => element.Label == "3:2");
        cell.Role.Should().Be("AXCell");
        SendRange(cell.Element, Sel("accessibilityRowIndexRange")).Location.Should().Be((nuint)3);
        SendRange(cell.Element, Sel("accessibilityColumnIndexRange")).Location.Should().Be((nuint)2);
        said.Single(element => element.Label == "0:2").Role.Should().Be("AXCell", "a header is a cell to AppKit");
    }

    /// <summary>A heading's level, read back as AppKit holds it: VoiceOver says "heading level 2".</summary>
    [MacFact]
    public void AHeadingReachesAppKitWithItsLevel()
    {
        IReadOnlyList<SemanticNode> tree =
            [new(SemanticRole.StaticText, 1, new Rect(0, 0, 120, 24), "Revenue", null, false, HeadingLevel: 2)];
        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var element = Send(PhotonAccessibility.BuildChildren(view), Sel("objectAtIndex:"), (nuint)0);
            FromNSString(Send(element, Sel("accessibilityRole"))).Should().Be("AXHeading");
            SendLong(element, Sel("accessibilityDisclosureLevel")).Should().Be(2);
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    /// <summary>A tree's item, read back as AppKit holds it (#1529): an outline row, whose disclosure level
    /// counts from zero where TreePlace counts from one, open and picked as the tree said.</summary>
    [MacFact]
    public void ATreeItemReachesAppKitAsAnOutlineRowWithItsLevel()
    {
        IReadOnlyList<SemanticNode> tree =
        [
            new(SemanticRole.Tree, 1, new Rect(0, 0, 240, 64), "Files", null, false),
            new(SemanticRole.TreeItem, 2, new Rect(0, 0, 240, 32), "src", null, false, Expanded: true, Selected: false,
                TreePlace: new TreePlace(1, 1, 1)),
            new(SemanticRole.TreeItem, 3, new Rect(0, 32, 240, 32), "App.cs", null, false, Selected: true,
                TreePlace: new TreePlace(2, 1, 1)),
        ];
        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var children = PhotonAccessibility.BuildChildren(view);
            var outline = Send(children, Sel("objectAtIndex:"), (nuint)0);
            FromNSString(Send(outline, Sel("accessibilityRole"))).Should().Be("AXOutline");
            var file = Send(children, Sel("objectAtIndex:"), (nuint)2);
            FromNSString(Send(file, Sel("accessibilityRole"))).Should().Be("AXRow");
            FromNSString(Send(file, Sel("accessibilitySubrole"))).Should().Be("AXOutlineRow");
            SendLong(file, Sel("accessibilityDisclosureLevel")).Should().Be(1, "level 2 is AppKit's level 1");
            SendLong(Send(children, Sel("objectAtIndex:"), (nuint)1), Sel("accessibilityDisclosureLevel")).Should().Be(0);
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    [MacFact]
    public void AModalLayerReachesAppKitAsADialog()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Text("Cards", TypeRole.Title));
        page.Add(new Overlay(new Text("Are you sure?", TypeRole.BodyM)) { Label = "Confirm" });

        var dialog = ReadBack(page).Single(element => element.Label == "Confirm");

        (dialog.Role, dialog.Subrole).Should().Be(("AXGroup", "AXApplicationDialog"));
    }

    /// <summary>The element each build hands AppKit for a node, by the node's id.</summary>
    private static Dictionary<string, IntPtr> ElementsOf(IntPtr children)
    {
        var found = new Dictionary<string, IntPtr>();
        var count = SendULong(children, Sel("count"));
        for (nuint index = 0; index < count; index++)
        {
            var element = Send(children, Sel("objectAtIndex:"), index);
            found[FromNSString(Send(element, Sel("accessibilityIdentifier")))!] = element;
        }
        return found;
    }

    /// <summary>
    /// A node's element lives as long as the node (#1111): read again, the tree hands AppKit the same element,
    /// told what the node says now. Each read built new elements and released the ones before, so the element
    /// VoiceOver's cursor stood on died the next time anything read the tree, and its walk stopped at the
    /// title bar. A node that leaves takes its element with it, and a position that holds another kind of
    /// node is a new element.
    /// </summary>
    [MacFact]
    public void ANodesElementLivesAsLongAsTheNode_ToldWhatItSaysNow()
    {
        SemanticNode NodeOf(SemanticRole role, int id, string name, string? value = null) =>
            new(role, id, new Rect(0, id * 30, 120, 24), name, value, false);
        List<SemanticNode> tree =
        [
            NodeOf(SemanticRole.Button, 1, "Save"),
            NodeOf(SemanticRole.TextField, 2, "Name", "Ana"),
            NodeOf(SemanticRole.Button, 3, "Delete"),
        ];
        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var first = ElementsOf(PhotonAccessibility.BuildChildren(view));

            tree[1] = NodeOf(SemanticRole.TextField, 2, "Name", "Ana Lima");
            tree[2] = NodeOf(SemanticRole.Checkbox, 3, "Delete");
            tree.RemoveAt(0);
            var second = ElementsOf(PhotonAccessibility.BuildChildren(view));

            second["2"].Should().Be(first["2"], "the field is the same node, so VoiceOver's cursor on it stays valid");
            FromNSString(Send(second["2"], Sel("accessibilityValue"))).Should().Be("Ana Lima", "and it says what it holds now");
            second.Should().NotContainKey("1", "a node that left takes its element with it");
            second["3"].Should().NotBe(first["3"], "a position that holds another kind of node is another element");
            FromNSString(Send(second["3"], Sel("accessibilityRole"))).Should().Be("AXCheckBox");
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    /// <summary>What a node stops saying, its element stops saying: a value, a pick and an open list, each taken
    /// away rather than left from the read before, now that an element outlives a read (#1111).</summary>
    [MacFact]
    public void WhatANodeStopsSaying_ItsElementStopsSaying()
    {
        List<SemanticNode> tree =
            [new(SemanticRole.Tab, 1, new Rect(0, 0, 120, 24), "Overview", "1 of 2", false, Selected: true, Expanded: true)];
        var previous = PhotonAccessibility.Source;
        PhotonAccessibility.Source = () => tree;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            var element = ElementsOf(PhotonAccessibility.BuildChildren(view))["1"];
            FromNSString(Send(element, Sel("accessibilityValue"))).Should().Be("1 of 2", "the first read told it all three");
            SendBool(element, Sel("isAccessibilitySelected")).Should().BeTrue();
            SendBool(element, Sel("isAccessibilityExpanded")).Should().BeTrue();
            tree[0] = new(SemanticRole.Tab, 1, new Rect(0, 0, 120, 24), "Overview", null, false, Selected: false, Expanded: false);
            ElementsOf(PhotonAccessibility.BuildChildren(view))["1"].Should().Be(element);

            Send(element, Sel("accessibilityValue")).Should().Be(IntPtr.Zero, "the value it no longer has is gone");
            SendBool(element, Sel("isAccessibilitySelected")).Should().BeFalse("the tab is no longer picked");
            SendBool(element, Sel("isAccessibilityExpanded")).Should().BeFalse("and nothing under it is open");

            // A node that stops having an open-or-closed state at all is a new element, since AppKit keeps the
            // state it was told: the old one would read "collapsed".
            tree[0] = new(SemanticRole.Tab, 1, new Rect(0, 0, 120, 24), "Overview", null, false, Selected: false);
            ElementsOf(PhotonAccessibility.BuildChildren(view))["1"].Should().NotBe(element);
        }
        finally
        {
            PhotonAccessibility.Source = previous;
        }
    }

    /// <summary>
    /// The window's content is a group AppKit lists, and its focused element is the element of the node with
    /// the keyboard (#1111). The view was no element, so the window listed the elements while each named a
    /// parent nothing listed, and the focused element was the window itself. Read through the real view's own
    /// dispatch, as AppKit asks it.
    /// </summary>
    [MacFact]
    public void TheContentIsAGroup_WhoseFocusedElementHoldsTheKeyboard()
    {
        IReadOnlyList<SemanticNode> tree =
        [
            new(SemanticRole.Button, 1, new Rect(0, 0, 120, 24), "Save", null, false),
            new(SemanticRole.TextField, 2, new Rect(0, 30, 120, 24), "Name", "Ana", false),
        ];
        int? focused = null;
        var previousSource = PhotonAccessibility.Source;
        var previousFocus = PhotonAccessibility.FocusedId;
        PhotonAccessibility.Source = () => tree;
        PhotonAccessibility.FocusedId = () => focused;
        try
        {
            var view = Send(Send(PhotonContentView.Register(), Sel("alloc")), Sel("init"));
            SendBool(view, Sel("isAccessibilityElement")).Should().BeTrue("the group the window lists, its elements' parent");
            FromNSString(Send(view, Sel("accessibilityRole"))).Should().Be("AXGroup");
            var elements = ElementsOf(Send(view, Sel("accessibilityChildren")));
            Send(elements["2"], Sel("accessibilityParent")).Should().Be(view);

            Send(view, Sel("accessibilityFocusedUIElement")).Should().Be(view, "nothing holds the keyboard yet");
            focused = 2;
            Send(view, Sel("accessibilityFocusedUIElement")).Should().Be(elements["2"],
                "the field with the keyboard is the focused element, the same one the group lists");

            // Asked again after the field's text changed and before anything read the group, the focused element
            // says what the field holds now.
            tree = [tree[0], new(SemanticRole.TextField, 2, new Rect(0, 30, 120, 24), "Name", "Ana Lima", false)];
            var focusedNow = Send(view, Sel("accessibilityFocusedUIElement"));
            focusedNow.Should().Be(elements["2"]);
            FromNSString(Send(focusedNow, Sel("accessibilityValue"))).Should().Be("Ana Lima");
        }
        finally
        {
            PhotonAccessibility.Source = previousSource;
            PhotonAccessibility.FocusedId = previousFocus;
        }
    }

    /// <summary>
    /// VoiceOver is told where the keyboard goes once per move (#1111): on the element of the node that holds
    /// it, or, while that node has no element yet, once on the view and once more when a read of the tree
    /// gives the node one. A position that comes to hold another kind of node is a new element, and the
    /// keyboard on it is told again. Read from what the bridge posts, since AppKit keeps it to itself.
    /// </summary>
    [MacFact]
    public void TheKeyboardMoving_IsToldOnce_AndAgainWhenItsNodeGetsAnElement()
    {
        List<SemanticNode> tree = [new(SemanticRole.Button, 1, new Rect(0, 0, 120, 24), "Save", null, false)];
        int? focused = null;
        var told = new List<(IntPtr Element, string Name)>();
        void Hear(IntPtr element, string name) => told.Add((element, name));
        var previousSource = PhotonAccessibility.Source;
        var previousFocus = PhotonAccessibility.FocusedId;
        PhotonAccessibility.Source = () => tree;
        PhotonAccessibility.FocusedId = () => focused;
        AppKitNotifications.Posted += Hear;
        try
        {
            var view = Send(Send(AppKit.Class("NSView"), Sel("alloc")), Sel("init"));
            PhotonAccessibility.BuildChildren(view);
            PhotonAccessibility.FocusMoved(view);
            told.Should().BeEmpty("the keyboard has not moved");

            // The keyboard lands on a field the tree does not hold yet: told once, on the view, however many
            // frames draw before the field arrives.
            focused = 2;
            for (var frame = 0; frame < 5; frame++) PhotonAccessibility.FocusMoved(view);
            told.Select(said => said.Element).Should().Equal([view]);

            // The field arrives, and the read that gives it an element tells VoiceOver so.
            tree.Add(new(SemanticRole.TextField, 2, new Rect(0, 30, 120, 24), "Name", "Ana", false));
            var field = ElementsOf(PhotonAccessibility.BuildChildren(view))["2"];
            PhotonAccessibility.FocusMoved(view);
            told.Select(said => said.Element).Should().Equal([view, field]);

            // The position comes to hold another kind of node: a new element, told on the next frame.
            tree[1] = new(SemanticRole.Checkbox, 2, new Rect(0, 30, 120, 24), "Remember", null, false);
            var box = ElementsOf(PhotonAccessibility.BuildChildren(view))["2"];
            PhotonAccessibility.FocusMoved(view);
            told.Select(said => said.Element).Should().Equal([view, field, box]);
            told.Should().OnlyContain(said => said.Name == "NSAccessibilityFocusedUIElementChangedNotification");
        }
        finally
        {
            AppKitNotifications.Posted -= Hear;
            PhotonAccessibility.Source = previousSource;
            PhotonAccessibility.FocusedId = previousFocus;
        }
    }

    /// <summary>A text field a listbox hangs from (#1088) reaches AppKit as NSComboBox's role, named by its label,
    /// holding its text as its value and saying whether its list shows: Core-AAM's pair for an editable
    /// combobox, which VoiceOver reads as a combo box a person types into.</summary>
    [MacFact]
    public void AFieldAListboxHangsFrom_ReachesAppKitAsAComboBox()
    {
        var list = new Column(gap: 0);
        list.Add(new Pressable(new Text("Alpha", TypeRole.BodyM), () => { }) { Role = PressableRole.Option });
        var page = new Anchored(new TextEntry("al", _ => { }) { Label = "Go to" }, list)
        {
            Open = true,
            PanelRole = AnchorPanelRole.Listbox,
            ActiveIndex = 0,
        };

        var field = ReadBack(page).Single(element => element.Label == "Go to");

        (field.Role, field.Subrole, field.Description).Should().Be(("AXComboBox", null, "combo box"));
        FromNSString(Send(field.Element, Sel("accessibilityValue"))).Should().Be("al", "its text is its value");
        SendBool(field.Element, Sel("isAccessibilityExpanded")).Should().BeTrue("and it says its list shows");
    }
}
