using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using EQuanticApp.Components;
using EQuanticApp.Resources;

namespace EQuanticApp.Pages;

/// <summary>
/// Your first write-once page: this C# renders on the server, compiles to JavaScript at build
/// time, and the SAME source realizes natively when you add a Photon shell. No JS was written.
/// <para>
/// Every name below is a factory in scope everywhere: the framework's, your own components'
/// (StatTile is in Components/) and the shell's — all generated from the components themselves.
/// </para>
/// </summary>
[Page("/", Title = "Overview")]
public sealed class HomePage : StatefulComponent
{
    private int _count;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        return AppShell("/", Column(gap: Space.S4, children: [
            Text("Overview", TypeRole.Display, theme.TextPrimary, maxLines: 1),
            // From Resources/Strings.resx — the ordinary .NET way. The server resolves it under the
            // request's culture; the browser resolves the SAME key from the emitted catalog.
            Text(Strings.Tagline, TypeRole.BodyM, theme.TextSecondary, maxLines: 2),

            Row(gap: Space.S3, wrap: true, children: [
                StatTile("Count", $"{_count}"),
                StatTile("Doubled", $"{_count * 2}"),
            ]),

            Row(gap: Space.S3, children: [
                Button("Count", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),

            // resx + string.Format + state, in one line. The build validates the {0} against every
            // culture's resx (a translation asking for {1} fails the BUILD, not a visitor).
            Text(string.Format(Strings.CountedTimes, _count), TypeRole.Caption, theme.TextMuted,
                maxLines: 1),
        ]));
    }
}
