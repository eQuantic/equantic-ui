using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A stateful input or picker is kept across its parent's rebuilds, so the style and the builder a parent
/// hands it later reach the kept instance through <c>AdoptConfig</c> (#1548): a property that is not adopted
/// keeps its first value, and the component keeps drawing what the first build asked for.
/// </summary>
public class InputStyleAdoptConfigTests
{
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));
    private static readonly ColorToken Other = new(Color.FromRgb(0x65, 0x43, 0x21));

    private static InputTheme Field(ColorToken color) => new() { BorderColor = color, Fill = color, SearchFill = color };

    /// <summary>One stateful component, built twice: the first configuration and the second a parent hands the
    /// kept instance, and the word each writes into the page.</summary>
    public sealed record Case(string Name, Func<ColorToken, VisualNode> Build) // marker is the colour, in CSS
    {
        public override string ToString() => Name;
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("TextInput", c => new TextInput("ana", _ => { }, style: Field(c))),
        new Case("Select", c => new Select(["A"], 0, _ => { }, style: Field(c))),
        new Case("TimePicker", c => new TimePicker(null, _ => { }, style: Field(c))),
        new Case("DatePicker", c => new DatePicker(null, _ => { }, style: Field(c))),
        new Case("DateTimePicker", c => new DateTimePicker(null, _ => { }, style: Field(c))),
        new Case("Calendar", c => new Calendar(null, _ => { }, style: new CalendarTheme { DayColor = c, TitleColor = c })),
        new Case("DatePicker's calendarStyle", c =>
            new DatePicker(null, _ => { }, calendarStyle: new CalendarTheme { OpenerColor = c })),
        new Case("DateTimePicker's calendarStyle", c =>
            new DateTimePicker(null, _ => { }, calendarStyle: new CalendarTheme { OpenerColor = c })),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AParentThatRebuildsWithANewStyle_ReachesTheKeptInstance(Case c)
    {
        using var tester = new ComponentTester(PhotonTheme.Instance);
        tester.Pump(c.Build(Ink));
        tester.Html.Should().NotBeNull();
        tester.Css.Should().Contain("#123456").And.NotContain("#654321");

        tester.Pump(c.Build(Other));

        tester.Css.Should().Contain("#654321", "the second configuration's style reaches the instance the position kept");
    }

    [Fact]
    public void AParentThatRebuildsWithANewBuilder_ReachesTheKeptSelect()
    {
        using var tester = new ComponentTester(PhotonTheme.Instance);
        tester.Pump(new Select(["Lisboa"], 0, _ => { }, optionBuilder: _ => new Text("first")));
        tester.Tap(SemanticRole.ComboBox, "Lisboa");
        tester.Pump();
        tester.Html.Should().Contain("first");

        tester.Pump(new Select(["Lisboa"], 0, _ => { }, optionBuilder: _ => new Text("second")));

        tester.Html.Should().Contain("second").And.NotContain("first");
    }
}
