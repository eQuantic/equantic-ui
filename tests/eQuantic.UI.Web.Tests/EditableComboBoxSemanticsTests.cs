using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A text control that is a combobox is read as the EDITABLE combobox it is (#1088): the web writes
/// <c>role="combobox"</c> on the text control of a field a listbox hangs from (#1083), and a reader types into
/// it while the arrows walk its list. The testing reader heard it as a select-only combobox named by nothing,
/// where Photon, which says what the field is, has the editable one: the parity fixture could not compare them.
/// </summary>
public class EditableComboBoxSemanticsTests
{
    private static VisualNode GoTo(bool open)
    {
        var list = new Column(gap: 0);
        list.Add(new Pressable(new Text("Alpha", TypeRole.BodyM), () => { }) { Role = PressableRole.Option });
        list.Add(new Pressable(new Text("Beta", TypeRole.BodyM), () => { }) { Role = PressableRole.Option });
        return new Anchored(new TextEntry("al", _ => { }) { Label = "Go to" }, list)
        {
            Open = open,
            PanelRole = AnchorPanelRole.Listbox,
            ActiveIndex = 1,
        };
    }

    [Fact]
    public void AFieldAListboxHangsFrom_IsAnEditableComboBox_NamedByItsLabel_HoldingItsText()
    {
        using var tester = new ComponentTester();

        tester.Pump(GoTo(open: true));

        tester.Semantics[0].Should().Match<Announcement>(field => field.Role == SemanticRole.EditableComboBox
            && field.Name == "Go to" && field.Value == "al" && field.Expanded == true);
    }

    [Fact]
    public void AClosedListSaysSo_AndASelectStaysTheSelectOnlyComboBox()
    {
        using var tester = new ComponentTester();

        tester.Pump(GoTo(open: false));
        tester.Semantics[0].Should().Match<Announcement>(field => field.Role == SemanticRole.EditableComboBox
            && field.Expanded == false);

        tester.Pump(new eQuantic.UI.Components.Select(["Lisbon", "Porto"], 0, _ => { }));
        tester.Semantics.Should().ContainSingle(stop => stop.Role == SemanticRole.ComboBox,
            "a select's trigger is a button the combobox role is written on, and nobody types into it");
    }
}
