using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using EQuanticApp.Resources;

namespace EQuanticApp.Pages;

/// <summary>
/// Your first write-once page: this C# renders on the server, compiles to JavaScript at build
/// time, and the SAME source realizes natively when you add a Photon shell. No JS was written.
/// <para>
/// The interface is plain C# expressions — no markup language, no builder ceremony. Every name
/// below is a factory in scope everywhere: the framework's, and your own components' (StatTile is
/// in Components/, and its factory is generated from the component itself).
/// </para>
/// </summary>
[Page("/", Title = "EQuanticApp")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    // The languages this app ships — one resx file per entry, nothing else. The switcher below
    // re-renders THIS page in place (no reload) and tells the server for the next request.
    private static readonly CultureOption[] Languages =
    [
        new("en", "English"),
        new("pt-BR", "Português"),
    ];

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("EQuanticApp", TypeRole.Display, theme.TextPrimary, maxLines: 1),
            // From Resources/Strings.resx — the ordinary .NET way. The server resolves it under
            // the request's culture; the browser resolves the SAME key from the catalog the build
            // emitted. `{0}` composites work too: see the counter line below.
            Text(Strings.Tagline, TypeRole.BodyM, theme.TextSecondary, maxLines: 2),

            CultureSwitcher(Languages),

            // Your own component, composed exactly like the framework's.
            Row(gap: Space.S3, children: [
                StatTile("Count", $"{_count}"),
                StatTile("Doubled", $"{_count * 2}"),
            ]),

            Row(gap: Space.S3, children: [
                Button("Count", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),

            // resx + string.Format + state, in one line: the template's whole story. The build
            // validates the {0} against every culture's resx (a translation that asks for {1}
            // fails the BUILD, not a visitor), and switching language re-formats it live.
            Text(string.Format(Strings.CountedTimes, _count), TypeRole.Caption, theme.TextMuted,
                maxLines: 1),
        ]));
    }
}
