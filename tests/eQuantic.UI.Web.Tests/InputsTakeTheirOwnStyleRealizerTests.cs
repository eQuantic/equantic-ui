using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An input, a picker or a segmented control takes its family's slot record as a style of its own (#1548,
/// the catalog's decision 6): the instance's record is laid over the theme's slot with the record's
/// <c>Merge</c>, so what it sets wins over a different value the theme has, and a sibling with no style keeps
/// the theme's. The web's half; Photon's is <c>InputsTakeTheirOwnStyleTests</c>. Read from the markup and the
/// atomic stylesheet the lowering writes.
/// </summary>
public class InputsTakeTheirOwnStyleRealizerTests
{
    private static readonly ColorToken ThemeInk = new(Color.FromRgb(0xAB, 0x00, 0x01));
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));
    private const string ThemeCss = "#ab0001";
    private const string OwnCss = "#123456";

    private static readonly IAppTheme Photon = PhotonTheme.Instance;

    private static InputTheme Input(ColorToken color) => new() { BorderColor = color, Fill = color, SearchFill = color };

    /// <summary>A case: the theme, the styled component and its sibling with no style, and the two words each
    /// writes into the page (the instance's and the theme's).</summary>
    public sealed record Case(string Name, Func<IAppTheme> Theme, Func<VisualNode> Styled, Func<VisualNode> Plain,
        string Own = OwnCss, string Themed = ThemeCss, bool Open = false)
    {
        public override string ToString() => Name;
    }

    private static FormController Form()
    {
        var form = new FormController();
        form.Add("email", rules: [Rules.Required()]);
        return form;
    }

    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.Today);

    public static TheoryData<Case> Cases => new()
    {
        new Case("TextInput", () => Photon.With(input: Input(ThemeInk)),
            () => new TextInput("ana", _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new TextInput("ana", _ => { })),
        new Case("FormInput hands its style down", () => Photon.With(input: Input(ThemeInk)),
            () => new FormInput(Form(), "email", "Email", style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new FormInput(Form(), "email", "Email")),
        new Case("SearchField", () => Photon.With(input: Input(ThemeInk)),
            () => new SearchField("", _ => { }, style: new InputTheme { SearchFill = Ink }),
            () => new SearchField("", _ => { })),
        new Case("Select", () => Photon.With(input: Input(ThemeInk)),
            () => new Select(["A", "B"], 0, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new Select(["A", "B"], 0, _ => { })),
        new Case("TimePicker", () => Photon.With(input: Input(ThemeInk)),
            () => new TimePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new TimePicker(null, _ => { })),
        new Case("DatePicker's field", () => Photon.With(input: Input(ThemeInk)),
            () => new DatePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new DatePicker(null, _ => { })),
        new Case("DateTimePicker's two fields", () => Photon.With(input: Input(ThemeInk)),
            () => new DateTimePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new DateTimePicker(null, _ => { })),
        new Case("Calendar", () => Photon.With(calendar: new CalendarTheme { CellSize = 30 }),
            () => new Calendar(Day, _ => { }, style: new CalendarTheme { CellSize = 46 }),
            () => new Calendar(Day, _ => { }), Own: "width:46px", Themed: "width:30px"),
        new Case("DatePicker's calendarStyle, opened",
            () => Photon.With(calendar: new CalendarTheme { PanelFill = ThemeInk }),
            () => new DatePicker(null, _ => { }, calendarStyle: new CalendarTheme { PanelFill = Ink }),
            () => new DatePicker(null, _ => { }), Open: true),
        new Case("DateTimePicker's calendarStyle, opened",
            () => Photon.With(calendar: new CalendarTheme { PanelFill = ThemeInk }),
            () => new DateTimePicker(null, _ => { }, calendarStyle: new CalendarTheme { PanelFill = Ink }),
            () => new DateTimePicker(null, _ => { }), Open: true),
        new Case("SegmentedControl", () => Photon.With(segmented: new SegmentedTheme { ThumbFill = ThemeInk }),
            () => new SegmentedControl(["A", "B"], 0, _ => { }, style: new SegmentedTheme { ThumbFill = Ink }),
            () => new SegmentedControl(["A", "B"], 0, _ => { })),
        new Case("Stepper", () => Photon.With(segmented: new SegmentedTheme { BorderColor = ThemeInk }),
            () => new Stepper(2, _ => { }, style: new SegmentedTheme { BorderColor = Ink }),
            () => new Stepper(2, _ => { })),
        new Case("CultureSwitcher hands its style to its segments",
            () => Photon.With(segmented: new SegmentedTheme { ThumbFill = ThemeInk }),
            () => new CultureSwitcher([new CultureOption("en", "English"), new CultureOption("pt", "Português")],
                new SegmentedTheme { ThumbFill = Ink }),
            () => new CultureSwitcher([new CultureOption("en", "English"), new CultureOption("pt", "Português")])),
    };

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    /// <summary>The page a component lowers to, opened first when asked. An opened one is drawn by a
    /// tester, which keeps the element tree between the press and the frame that draws the panel: a
    /// picker a component builds inside itself (the DateTimePicker's date field) is a new, closed
    /// instance in a second lowering of its own.</summary>
    private static string Lowered(VisualNode node, IAppTheme theme, bool open)
    {
        if (open)
        {
            using var tester = new ComponentTester(theme);
            tester.Pump(node);
            var trigger = Walk(tester.Root).First(n => n.Attributes.ContainsKey("aria-expanded") && n.Events.ContainsKey("click"));
            ((Action)trigger.Events["click"])();
            tester.Pump();
            return tester.Html + tester.Css;
        }

        var sink = new StyleSink();
        var html = HtmlRenderer.RenderNode(WebRealizer.Lower(node, theme, 1f, sink).Render());
        return html + sink.Css;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AStyleTheInstanceSets_WinsOverTheThemes_AndASiblingKeepsTheThemes(Case c)
    {
        var theme = c.Theme();
        var styled = Lowered(c.Styled(), theme, c.Open);
        var plain = Lowered(c.Plain(), theme, c.Open);

        styled.Should().Contain(c.Own, "the instance's own style is what it draws");
        styled.Should().NotContain(c.Themed, "and not the theme's value it replaces");
        plain.Should().Contain(c.Themed, "a sibling with no style draws the theme's");
        plain.Should().NotContain(c.Own, "and not its neighbour's");
    }

    /// <summary>The upload area draws what the slot names, from the merged slot before the theme's tokens: a
    /// style it is handed reaches its border and fill, and a theme's slot reaches an area with none.</summary>
    [Fact]
    public void AFileUploadsAreaReadsTheMergedSlotBeforeTheTokens()
    {
        var policy = new UploadPolicy("models", [], 1024);
        var theme = Photon.With(input: new InputTheme { BorderColor = ThemeInk });

        var styled = Lowered(new FileUpload(policy, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }), theme, false);
        var plain = Lowered(new FileUpload(policy, _ => { }), theme, false);

        styled.Should().Contain(OwnCss).And.NotContain(ThemeCss, "the area's border and fill are the style's");
        plain.Should().Contain(ThemeCss, "an area with no style takes the theme's slot").And.NotContain(OwnCss);
    }
}
