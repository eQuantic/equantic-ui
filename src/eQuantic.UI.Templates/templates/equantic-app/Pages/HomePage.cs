using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

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
            Text("C# in, pixels out — the counter below is a component, a state field and a handler.",
                TypeRole.BodyM, theme.TextSecondary, maxLines: 2),

            // Your own component, composed exactly like the framework's.
            Row(gap: Space.S3, children: [
                StatTile("Count", $"{_count}"),
                StatTile("Doubled", $"{_count * 2}"),
            ]),

            Row(gap: Space.S3, children: [
                Button("Count", onPressed: () => SetState(() => _count++)),
                Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
            ]),
        ]));
    }
}
