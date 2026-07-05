using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>Card surfaces (spec B1).</summary>
public enum CardKind : byte
{
    /// <summary>E1 shadow; dark mode ALSO gets a 1dp Border (§05).</summary>
    Elevated = 0,
    /// <summary>E0 + Border — quiet container for dense grids.</summary>
    Outlined = 1,
    /// <summary>SurfaceSubtle fill, E0 — inset grouping inside other surfaces.</summary>
    Filled = 2,
}

/// <summary>
/// The design system's Card (spec B1): Radius.Lg surface, S4 padding (S5 for hero cards),
/// header/body composition owned by the caller (compose a <see cref="Column"/> with gap S3).
/// Don't nest Elevated in Elevated — inner containers go Outlined/Filled (spec B1).
/// </summary>
public sealed class Card : StatelessComponent
{
    public Card(VisualNode child, CardKind kind = CardKind.Elevated)
    {
        Child = child;
        Kind = kind;
    }

    public VisualNode Child { get; init; }
    public CardKind Kind { get; init; }

    /// <summary>S4 by default; S5 for hero cards (spec B1).</summary>
    public EdgeInsets Padding { get; init; } = EdgeInsets.All(Space.S4);

    /// <summary>Hug by default; Fill for full-width list cards.</summary>
    public SizeValue Width { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var background = Kind == CardKind.Filled ? theme.SurfaceSubtle : theme.Surface;
        var borderWidth = Kind == CardKind.Outlined ? 1f : Kind == CardKind.Elevated ? 1f : 0f;
        // §05: Elevated = E1 shadow, and DARK mode also wants the 1dp border — a mode-free token
        // with a transparent light side realizes exactly that on every target.
        var borderColor = Kind == CardKind.Elevated
            ? new ColorToken(Color.Transparent, theme.Border.Dark)
            : theme.Border;

        return new Box(new BoxStyle
        {
            Width = Width,
            Padding = Padding,
            Background = background,
            CornerRadius = new CornerRadii(Radius.Lg),
            BorderWidth = borderWidth,
            BorderColor = borderColor,
            Elevation = Kind == CardKind.Elevated ? 1 : 0,
        }, Child);
    }
}
