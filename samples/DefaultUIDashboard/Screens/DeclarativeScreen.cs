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

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/declarative", "Declarative", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
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
