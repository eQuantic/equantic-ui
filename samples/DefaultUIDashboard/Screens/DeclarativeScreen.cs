using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// The DECLARATIVE authoring proof: the whole screen is factory calls — not a single <c>new</c>
/// for nodes or components, and not a single <c>using static</c> either (the SDK injects the
/// factory surface globally). This is the exact counter the docs and the announcement show;
/// value data (BoxStyle) keeps target-typed <c>new</c> by design — it is data, not tree.
/// </summary>
[Page("/declarative", Title = "Declarative counter — eQuantic Console")]
public sealed class DeclarativeScreen : StatefulComponent
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
            Text($"Count: {_count}", TypeRole.Display, theme.TextPrimary),
            Text("Declarative all the way down — factories, no new.", TypeRole.BodyM, theme.TextSecondary),
            Row(gap: Space.S2, children: [
                Button("Up", onPressed: () => SetState(() => _count++)),
                Button("Down", Variant.Secondary, onPressed: () => SetState(() => _count--)),
            ]),
        ]));
    }
}
