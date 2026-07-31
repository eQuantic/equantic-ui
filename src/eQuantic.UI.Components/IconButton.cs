using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>IconButton kinds (spec A13).</summary>
public enum IconButtonKind : byte
{
    /// <summary>No fill — toolbars and AppBars.</summary>
    Standard = 0,
    /// <summary>Subtle tinted container — emphasized secondary actions.</summary>
    Tonal = 1,
    /// <summary>Primary fill — a compact FAB stand-in only.</summary>
    Filled = 2,
    /// <summary>1dp BorderStrong outline.</summary>
    Outline = 3,
}

/// <summary>
/// The design system's IconButton (spec A13): a square icon control with a REQUIRED accessibility
/// label ("an unlabeled icon button does not compile" — the constructor demands it). Sizes
/// 32/40/48/56 with icons 16/20/24/24 (§07 whitelist); hit ≥ 48 always (the Pressable contract).
/// Toggle semantics via <see cref="Selected"/>: the glyph swaps to <see cref="SelectedGlyph"/>
/// (outline → filled pairs are DISTINCT glyphs, spec A10). Press physics identical to Button.
/// </summary>
public sealed class IconButton : StatelessComponent
{
    public IconButton(Icons glyph, string label, IconButtonKind kind = IconButtonKind.Standard,
        SizeVariant size = SizeVariant.Medium, Action? onPressed = null)
    {
        Glyph = glyph;
        Label = label;
        Kind = kind;
        Size = size;
        OnPressed = onPressed;
    }

    public Icons Glyph { get; init; }
    public string Label { get; init; }
    public IconButtonKind Kind { get; init; }
    public SizeVariant Size { get; init; }
    public Action? OnPressed { get; init; }
    public bool Disabled { get; init; }

    /// <summary>Toggle state — swaps the glyph to <see cref="SelectedGlyph"/> and tints Primary.</summary>
    public bool Selected { get; init; }

    /// <summary>The filled counterpart glyph shown while selected (outline↔filled are distinct glyphs).</summary>
    public Icons? SelectedGlyph { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var primary = theme.Colors(Variant.Primary);
        var side = Size switch
        {
            SizeVariant.Small => 32f,
            SizeVariant.Medium => 40f,
            SizeVariant.Large => 48f,
            _ => 56f,
        };
        var iconSize = Size switch
        {
            SizeVariant.Small => IconSize.Sm,
            SizeVariant.Medium => IconSize.Dense,
            _ => IconSize.Md,
        };

        ColorToken? fill = Kind switch
        {
            IconButtonKind.Tonal => primary.Subtle,
            IconButtonKind.Filled => primary.Base,
            _ => null,
        };
        var tint = Kind switch
        {
            IconButtonKind.Filled => primary.OnBase,
            IconButtonKind.Tonal => primary.OnSubtle,
            _ => Selected ? primary.Base : theme.TextSecondary,
        };
        if (Disabled)
        {
            var opacity = theme.DisabledOpacity;
            if (fill is { } filled) fill = filled.WithOpacity(opacity);
            tint = tint.WithOpacity(opacity);
        }

        var glyph = Selected && SelectedGlyph is { } filledGlyph ? filledGlyph : Glyph;
        var content = new Row(gap: 0) { Main = MainAlign.Center, Height = SizeValue.Fill };
        content.Add(new Icon(glyph, iconSize, tint));

        var box = new Box(new BoxStyle
        {
            Width = side,
            Height = side,
            Background = fill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = Kind == IconButtonKind.Outline ? 1f : 0f,
            BorderColor = theme.BorderStrong,
        }, content);

        ColorToken? pressedFill = Kind switch
        {
            IconButtonKind.Filled => primary.Pressed,
            IconButtonKind.Tonal => primary.Pressed.WithOpacity(0.24f),
            _ => theme.SurfaceSubtle,
        };

        return new Pressable(box, Disabled ? null : OnPressed)
        {
            Disabled = Disabled,
            Label = Label,
            PressedBackground = Disabled ? null : pressedFill,
        };
    }
}
