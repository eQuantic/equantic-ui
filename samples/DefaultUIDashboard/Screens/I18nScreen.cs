using DefaultUIDashboard.Resources;
using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// Track L, live: the strings on this page come from <c>Resources/Strings.resx</c> (+ pt-BR, + es, + ar),
/// written exactly as any .NET app writes them. The server resolves them through the real
/// ResourceManager under the request culture; the browser resolves the SAME keys through the
/// culture catalog the build emitted — request it with <c>Accept-Language: pt-BR</c> and both
/// halves answer in Portuguese, byte-identical through hydration.
/// <para>
/// M1 is the switcher below: it changes the language WITHOUT a reload (the catalog swaps and the
/// tree re-renders), and tells the server through ASP.NET's own culture cookie so the next request
/// arrives already translated. The checkbox is here on purpose and says something this app never
/// authored: its announcement comes from the SDK's own resx, so it speaks Portuguese in a page
/// whose resx knows nothing about checkboxes (D14).
/// </para>
/// <para>
/// The section at the end is what follows the direction under Arabic (#1569), written once and with no test of the
/// direction anywhere in this file: the calendar's chevrons turn, the switch is dragged toward the end and the row
/// swiped toward the start, the arrows step the tabs in reading order, the strip scrolls from its start, and the code
/// keeps reading left to right. <c>tests/e2e/tests/direction.spec.ts</c> drives each one in a real browser.
/// </para>
/// </summary>
[Page("/i18n", Title = "Localization — eQuantic Console")]
public sealed class I18nScreen : StatefulComponent
{
    private string _name = "Ana";
    private bool _agreed = true;
    private string _switched = "";
    private DateOnly _day = new(2026, 8, 13);
    private bool _on;
    private bool _swiped;
    private int _tab;
    private float _scrolled;
    private bool _folded = true;

    /// <summary>A method of three lines under its header, folded until its chevron is pressed.</summary>
    private const string Snippet = "void Greet(string name)\n{\n    Console.WriteLine(name);\n}";

    /// <summary>A fixed instant, so the line reads the same on every run — what changes with the
    /// culture is the PATTERN, which is the point.</summary>
    private static readonly DateTime Moment = new(2026, 8, 13, 15, 45, 7);

    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
        new("es", "Español"),
        // Right to left: switching to it mirrors the page with no reload (#1568).
        new("ar", "العربية"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(new Text(Strings.Hero_Title, TypeRole.Heading));
        column.Add(new Text(Strings.Hero_Body, TypeRole.BodyM, theme.TextSecondary));

        column.Add(new CultureSwitcher(Languages)
        {
            OnChanged = name => SetState(() => _switched = name),
        });

        column.Add(new TextInput(_name, value => SetState(() => _name = value),
            label: "Name", size: SizeVariant.Medium));
        column.Add(new Text(string.Format(Strings.Greeting, _name), TypeRole.Title));

        // The SDK's own chrome, localized by the SDK: this checkbox announces "Marcado" under
        // pt-BR without this app owning a single translation for it.
        // NO label on purpose: an unlabelled checkbox announces its STATE, and that announcement
        // is the SDK's own string — so this line is what a pt-BR reader hears as "Marcado" without
        // this app translating anything.
        column.Add(new Checkbox(_agreed, () => SetState(() => _agreed = !_agreed)));

        // M2, live: the SAME C# line prints the currency, the grouping and the date THIS culture
        // uses — R$ 1.234,50 and 13/08/2026 in pt-BR, $1,234.50 and 8/13/2026 in en-US — and the
        // switch above changes them without a reload, exactly as it changes the strings.
        column.Add(new Text($"{1234.5:C2} · {0.982:P1} · {Moment:d}", TypeRole.BodyM,
            theme.TextSecondary));

        if (_switched.Length > 0)
            column.Add(new Text($"switched to {_switched}", TypeRole.Caption, theme.TextMuted));

        column.Add(Direction(theme));

        return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S6) }, column);
    }

    /// <summary>
    /// What reads in the direction of the page (#1569), each control as any screen writes it: under Arabic the
    /// realizer turns and swaps what it must, and this method never asks which way the page reads.
    /// </summary>
    private Column Direction(IAppTheme theme)
    {
        var section = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        section.Add(new Text("Direction", TypeRole.Title));

        section.Add(new Calendar(_day, day => SetState(() => _day = day)));

        section.Add(new Switch(_on, () => SetState(() => _on = !_on)));

        section.Add(new SwipeableRow(new Text("Swipe to flag", TypeRole.BodyM), "Flag", Icons.Warning)
        {
            Open = _swiped,
            OnOpenChanged = open => SetState(() => _swiped = open),
        });
        section.Add(new Text(_swiped ? "row open" : "row closed", TypeRole.Caption, theme.TextMuted));

        section.Add(new Tabs(["First", "Second", "Third"], _tab, tab => SetState(() => _tab = tab)));

        var chips = new Row(gap: Space.S2);
        for (var i = 1; i <= 12; i++) chips.Add(new Chip($"Item {i}"));
        section.Add(new ScrollView(chips, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(320),
            OnScrolled = offset => SetState(() => _scrolled = offset),
        });
        section.Add(new Text($"scrolled {(int)_scrolled}", TypeRole.Caption, theme.TextMuted));

        section.Add(new CodeBlock(Snippet, "csharp")
        {
            Folds = [new CodeFold(0, 3)],
            FoldedLines = _folded ? [0] : [],
            OnFoldPressed = _ => SetState(() => _folded = !_folded),
        });

        return section;
    }
}
