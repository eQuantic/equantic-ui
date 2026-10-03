using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// Colours a component derives itself, in its own browser-side code: a brand faded and mixed with
/// white, painted and printed. The browser holds a <c>Color</c> as plain data, so every one of
/// these calls goes to the runtime's companion, and an instance method emitted as a method of the
/// value threw here after hydration while the server's page looked right (#494).
/// </summary>
[Page("/colors", Title = "Colors — eQuantic Console")]
public sealed class ColorScreen : StatefulComponent
{
    private static readonly Color Brand = Color.FromRgb(0xF8, 0x71, 0x71);

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/colors", "Colors", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private static VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var faded = Brand.WithOpacity(0.8f);
        var mixed = Brand.MidpointWith(Color.White);
        Color made = new(0x3B, 0x82, 0xF6, 0xFF);

        return Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
            Padding = EdgeInsets.All(Space.S6),
        },
        Column(gap: Space.S4, children: [
            Text("Colours a component derives", TypeRole.Heading, theme.TextPrimary),
            Text("Faded, mixed and built in the browser-side code, painted and printed as .NET prints them.",
                TypeRole.BodyM, theme.TextSecondary),
            Swatch(theme, faded, "faded"),
            Swatch(theme, mixed, "mixed"),
            Swatch(theme, made, "made"),
        ]));
    }

    private static VisualNode Swatch(IAppTheme theme, Color color, string label) =>
        Row(gap: Space.S3, cross: CrossAlign.Center, children: [
            Box(new BoxStyle
            {
                Width = 44,
                Height = 44,
                Background = new ColorToken(color, color),
                CornerRadius = new CornerRadii(Radius.Md),
            }),
            Text($"{label}: {color}", TypeRole.BodyM, theme.TextPrimary),
        ]);
}
