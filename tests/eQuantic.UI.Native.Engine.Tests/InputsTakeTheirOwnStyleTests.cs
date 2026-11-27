using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An input, a picker or a segmented control takes its family's slot record as a style of its own (#1548,
/// the catalog's decision 6): the instance's record is laid over the theme's slot with the record's
/// <c>Merge</c>, so what it sets wins over a different value the theme has, and a sibling with no style keeps
/// the theme's. Photon's half; the web's is <c>InputsTakeTheirOwnStyleRealizerTests</c>. Read from what the
/// frame paints: a fill or a border in the colour, or a cell of the size.
/// </summary>
public class InputsTakeTheirOwnStyleTests
{
    private static readonly ColorToken ThemeInk = new(Color.FromRgb(0xAB, 0x00, 0x01));
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));
    private const float ThemeCell = 30f;
    private const float OwnCell = 46f;

    private static readonly IAppTheme Photon = PhotonTheme.Instance;

    private static InputTheme Input(ColorToken color) => new() { BorderColor = color, Fill = color, SearchFill = color };

    public sealed record Case(string Name, Func<IAppTheme> Theme, Func<VisualNode> Styled, Func<VisualNode> Plain,
        Func<DisplayList, ColorToken?, float?, bool> Marked, bool Open = false)
    {
        public override string ToString() => Name;
    }

    private static bool Paints(DisplayList list, ColorToken? token, float? _)
    {
        var color = token!.Value.Resolve(ThemeMode.Light, ContrastLevel.Standard);
        return list.Commands.ToArray().Any(c => (c.Kind is DrawCommandKind.FillRRect or DrawCommandKind.StrokeRRect) && c.Paint.Color == color);
    }

    /// <summary>A round cell of the size: the day the calendar marks as chosen.</summary>
    private static bool HasCell(DisplayList list, ColorToken? _, float? size) =>
        list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(c.Shape.Rect.Width - size!.Value) < 0.5f && MathF.Abs(c.Shape.Rect.Height - size.Value) < 0.5f);

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
            () => new TextInput("ana", _ => { }), Paints),
        new Case("FormInput hands its style down", () => Photon.With(input: Input(ThemeInk)),
            () => new FormInput(Form(), "email", "Email", style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new FormInput(Form(), "email", "Email"), Paints),
        new Case("NumberInput hands its style down", () => Photon.With(input: Input(ThemeInk)),
            () => new NumberInput(4, _ => { }, "Lead time", "days", style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new NumberInput(4, _ => { }, "Lead time", "days"), Paints),
        new Case("MoneyInput hands its style down", () => Photon.With(input: Input(ThemeInk)),
            () => new MoneyInput(1250, _ => { }, "Price", new Currency("USD", "$", 2), style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new MoneyInput(1250, _ => { }, "Price", new Currency("USD", "$", 2)), Paints),
        new Case("SearchField", () => Photon.With(input: Input(ThemeInk)),
            () => new SearchField("", _ => { }, style: new InputTheme { SearchFill = Ink }),
            () => new SearchField("", _ => { }), Paints),
        new Case("Select", () => Photon.With(input: Input(ThemeInk)),
            () => new Select(["A", "B"], 0, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new Select(["A", "B"], 0, _ => { }), Paints),
        new Case("TimePicker", () => Photon.With(input: Input(ThemeInk)),
            () => new TimePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new TimePicker(null, _ => { }), Paints),
        new Case("DatePicker's field", () => Photon.With(input: Input(ThemeInk)),
            () => new DatePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new DatePicker(null, _ => { }), Paints),
        new Case("DateTimePicker's two fields", () => Photon.With(input: Input(ThemeInk)),
            () => new DateTimePicker(null, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }),
            () => new DateTimePicker(null, _ => { }), Paints),
        new Case("Calendar", () => Photon.With(calendar: new CalendarTheme { CellSize = ThemeCell }),
            () => new Calendar(Day, _ => { }, style: new CalendarTheme { CellSize = OwnCell }),
            () => new Calendar(Day, _ => { }), HasCell),
        new Case("DatePicker's calendarStyle, opened",
            () => Photon.With(calendar: new CalendarTheme { PanelFill = ThemeInk }),
            () => new DatePicker(null, _ => { }, calendarStyle: new CalendarTheme { PanelFill = Ink }),
            () => new DatePicker(null, _ => { }), Paints, Open: true),
        new Case("DateTimePicker's calendarStyle, opened",
            () => Photon.With(calendar: new CalendarTheme { PanelFill = ThemeInk }),
            () => new DateTimePicker(null, _ => { }, calendarStyle: new CalendarTheme { PanelFill = Ink }),
            () => new DateTimePicker(null, _ => { }), Paints, Open: true),
        new Case("SegmentedControl", () => Photon.With(segmented: new SegmentedTheme { ThumbFill = ThemeInk }),
            () => new SegmentedControl(["A", "B"], 0, _ => { }, style: new SegmentedTheme { ThumbFill = Ink }),
            () => new SegmentedControl(["A", "B"], 0, _ => { }), Paints),
        new Case("Stepper", () => Photon.With(segmented: new SegmentedTheme { BorderColor = ThemeInk }),
            () => new Stepper(2, _ => { }, style: new SegmentedTheme { BorderColor = Ink }),
            () => new Stepper(2, _ => { }), Paints),
        new Case("CultureSwitcher hands its style to its segments",
            () => Photon.With(segmented: new SegmentedTheme { ThumbFill = ThemeInk }),
            () => new CultureSwitcher([new CultureOption("en", "English"), new CultureOption("pt", "Português")],
                new SegmentedTheme { ThumbFill = Ink }),
            () => new CultureSwitcher([new CultureOption("en", "English"), new CultureOption("pt", "Português")]), Paints),
    };

    private static DisplayList Draw(VisualNode node, IAppTheme theme, bool open)
    {
        var host = new PhotonHost(node, theme, ThemeMode.Light, 480, 640);
        host.RenderFrame(new DisplayListBuilder());
        if (open)
        {
            var trigger = host.Semantics().First(n => n.Expanded is not null);
            host.Activate(trigger.Id).Should().BeTrue("the picker opens from its trigger");
        }

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 16);
        return builder.Build();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AStyleTheInstanceSets_WinsOverTheThemes_AndASiblingKeepsTheThemes(Case c)
    {
        var theme = c.Theme();
        var isCell = c.Name == "Calendar";
        ColorToken? ownToken = isCell ? null : Ink;
        ColorToken? themeToken = isCell ? null : ThemeInk;
        float? own = isCell ? OwnCell : null;
        float? themed = isCell ? ThemeCell : null;

        var styled = Draw(c.Styled(), theme, c.Open);
        var plain = Draw(c.Plain(), theme, c.Open);

        c.Marked(styled, ownToken, own).Should().BeTrue("the instance's own style is what it draws");
        c.Marked(styled, themeToken, themed).Should().BeFalse("and not the theme's value it replaces");
        c.Marked(plain, themeToken, themed).Should().BeTrue("a sibling with no style draws the theme's");
        c.Marked(plain, ownToken, own).Should().BeFalse("and not its neighbour's");
    }

    /// <summary>The upload area draws what the slot names, from the merged slot before the theme's tokens: a
    /// style it is handed reaches its border and fill, and a theme's slot reaches an area with none.</summary>
    [Fact]
    public void AFileUploadsAreaReadsTheMergedSlotBeforeTheTokens()
    {
        var policy = new UploadPolicy("models", [], 1024);
        var theme = Photon.With(input: new InputTheme { BorderColor = ThemeInk });

        var styled = Draw(new FileUpload(policy, _ => { }, style: new InputTheme { BorderColor = Ink, Fill = Ink }), theme, false);
        var plain = Draw(new FileUpload(policy, _ => { }), theme, false);

        Paints(styled, Ink, null).Should().BeTrue("the area's border and fill are the style's");
        Paints(styled, ThemeInk, null).Should().BeFalse();
        Paints(plain, ThemeInk, null).Should().BeTrue("an area with no style takes the theme's slot");
    }
}
