using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// Excel's status bar, a component the app places beside its grid over the same controller (#984): while the
/// selection holds two or more cells in use, the Average and the Sum of the numbers among them and the Count of
/// them, in the page's language and culture; and while Excel's add mode (Shift+F8) is on, it says so. It reads
/// the engine's summary (<see cref="SheetController.Summary"/>), which a key that moves nothing does not read
/// again.
/// </summary>
public sealed class StatusBar : StatefulComponent
{
    /// <summary>What the bar keeps between its border and its text.</summary>
    public static EdgeInsets Padding => EdgeInsets.Symmetric(8, 4);

    private const TypeRole BarType = TypeRole.Caption;


    private StatusBar _config;

    /// <summary>What a refresh of the controller does here: build again, as one delegate.</summary>
    private readonly Action _rebuild;
    private SheetController? _listening;

    public StatusBar(SheetController controller)
    {
        Controller = controller;
        _config = this;
        _rebuild = () => SetState(() => { });
    }

    public SheetController Controller { get; private set; }

    /// <summary>How wide the bar is; the room it is given by default.</summary>
    public SizeValue Width { get; init; } = SizeValue.Fill;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not StatusBar fresh) return;
        if (_listening is not null) Listen(fresh.Controller);
        Controller = fresh.Controller;
        _config = fresh;
    }

    protected override void OnMount() => Listen(Controller);

    protected override void OnUnmount() => Listen(null);

    /// <summary>Listens to <paramref name="controller"/>'s refreshes, and to no other: a press in the grid
    /// changes what the bar sums.</summary>
    private void Listen(SheetController? controller)
    {
        if (ReferenceEquals(_listening, controller)) return;
        if (_listening is not null) _listening.Refreshed -= _rebuild;
        _listening = controller;
        if (controller is not null) controller.Refreshed += _rebuild;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var palette = SheetPalette.Of(theme);
        var type = theme.Type(BarType);
        var padding = Padding;

        var line = new Row(gap: 16);
        if (Controller.AddingRanges) line.Add(Words(SdkStrings.AddToSelection, palette));
        line.Add(new Spacer());
        var summary = Controller.Summary;
        if (summary.Count >= 2)
        {
            if (summary.Average is { } average) line.Add(Words(SdkStrings.Average(General(average)), palette));
            line.Add(Words(SdkStrings.Count(summary.Count), palette));
            if (summary.Numbers > 0 && summary.Sum is { } sum) line.Add(Words(SdkStrings.Sum(General(sum)), palette));
        }
        return new Box(new BoxStyle
        {
            Width = _config.Width,
            Height = SizeValue.Fixed(type.LineHeight + padding.Top + padding.Bottom + 2),
            Padding = padding,
            Background = palette.HeaderBackground,
            BorderColor = palette.Gridline,
            BorderWidth = 1f,
        }, line);
    }

    /// <summary>
    /// A number as Excel's General format writes it in its status bar, in the page's culture: in eleven
    /// characters, the places that fit after the whole part and its sign (`1.666666667`, `123456789.1`,
    /// `12345678901`), and in scientific notation past them (`1.23457E+11`, `-1.2346E+11`) or where it keeps
    /// more of the value than the places do (`1E-10`, where the places write `0`). A decimal's own text
    /// keeps 28 digits.
    /// </summary>
    private static string General(decimal value)
    {
        var negative = value < 0;
        // Eleven characters of scientific notation: six digits of mantissa, five beside a minus.
        var scientific = negative ? "0.####E+00" : "0.#####E+00";
        // The whole part's digits as General rounds it: 99999999999.6 is twelve digits, and goes scientific.
        var whole = (negative ? -value : value).ToString("0", CultureInfo.InvariantCulture).Length;
        var room = 11 - (negative ? 1 : 0) - whole;
        if (room < 0) return value.ToString(scientific, CultureInfo.CurrentCulture);
        var places = Math.Max(0, room - 1);
        var fixedText = value.ToString(places == 0 ? "0" : "0." + new string('#', places), CultureInfo.CurrentCulture);
        if (value == 0) return fixedText;
        var scientificText = value.ToString(scientific, CultureInfo.CurrentCulture);
        return Lost(value, scientificText) < Lost(value, fixedText) ? scientificText : fixedText;
    }

    /// <summary>How far <paramref name="text"/>, read back in the page's culture, stands from
    /// <paramref name="value"/>.</summary>
    private static decimal Lost(decimal value, string text)
    {
        var lost = value - decimal.Parse(text, NumberStyles.Float, CultureInfo.CurrentCulture);
        return lost < 0 ? -lost : lost;
    }

    private static Text Words(string text, SheetPalette palette) => new(text, BarType, palette.HeaderText, maxLines: 1);
}
