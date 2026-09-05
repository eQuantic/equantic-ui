using DefaultUIDashboard.Resources;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// Track L, live: the strings on this page come from <c>Resources/Strings.resx</c> (+ pt-BR, + es),
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
/// </summary>
[Page("/i18n", Title = "Localization — eQuantic Console")]
public sealed class I18nScreen : StatefulComponent
{
    private string _name = "Ana";
    private bool _agreed = true;
    private string _switched = "";

    /// <summary>A fixed instant, so the line reads the same on every run — what changes with the
    /// culture is the PATTERN, which is the point.</summary>
    private static readonly DateTime Moment = new(2026, 8, 13, 15, 45, 7);

    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
        new("es", "Español"),
    ];

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/i18n", "Localization", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
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

        return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S6) }, column);
    }
}
