using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Button (spec A12) — authored ONCE against the abstract vocabulary: a
/// <see cref="Pressable"/> wrapping a token-styled <see cref="Box"/> with a centered label
/// <see cref="Row"/>. Realizes as DOM+CSS on web and Photon pixels on native from this single
/// definition. Renders in 2–3 draws exactly as the spec budgets.
///
/// v1 (pre-interaction-system): the PRESSED/FOCUSED visuals are framework interaction states — they
/// swap tokens on the realized tree (fill → Pressed, focus double-ring) when the gesture/focus system
/// lands; this component authors the rest state. Loading (spinner, frozen width) joins with the
/// animation slice.
/// </summary>
public sealed class Button : StatelessComponent
{
    public Button(string label, Variant variant = Variant.Primary, SizeVariant size = SizeVariant.Medium,
        Action? onPressed = null)
    {
        Label = label;
        Variant = variant;
        Size = size;
        OnPressed = onPressed;
    }

    public string Label { get; init; }
    public Variant Variant { get; init; }
    public SizeVariant Size { get; init; }
    public Action? OnPressed { get; init; }

    /// <summary>Disabled = the 38% opacity group over the resolved style (spec §01) + presses swallowed.</summary>
    public bool Disabled { get; init; }

    /// <summary>Fills the parent row instead of hugging the label (checkout CTAs, spec A12).</summary>
    public bool Expand { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var colors = theme.Colors(Variant);
        var (height, padX, gap, labelSize, _, _, _) = ButtonStyles.Metrics(Size);
        // Shape is theme-driven (Material overrides the ladder): XLarge rides Large, the rest Medium.
        var radius = theme.Shape(Size == SizeVariant.XLarge ? ShapeScale.Large : ShapeScale.Medium);

        // Link is an inline-text control: no fill ever, minimal padding (spec A12).
        if (Variant == Variant.Link) padX = 6;
        ColorToken? fill = Variant is Variant.Outline or Variant.Ghost or Variant.Link ? null : colors.Base;
        var textColor = colors.OnBase;

        var borderWidth = Variant == Variant.Outline ? 1f : 0f;
        var borderColor = theme.BorderStrong;

        if (Disabled)
        {
            var opacity = theme.DisabledOpacity;
            if (fill is { } filled) fill = filled.WithOpacity(opacity);
            textColor = textColor.WithOpacity(opacity);
            borderColor = borderColor.WithOpacity(opacity);
        }

        // Label style comes from the spec's size table (13/15/16/17 · 600) — a system override, not a
        // free-form size (spec A8). Single line, ellipsis — never two lines (spec A12).
        var label = new Text(Label, TypeRole.Label, textColor, maxLines: 1)
        {
            StyleOverride = new TypeStyle(labelSize, labelSize, FontWeight.SemiBold, 0.1f, 1.3f),
        };

        var content = new Row(gap: gap)
        {
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
        };
        content.Add(label);

        var container = new Box(new BoxStyle
        {
            Height = height,
            Width = Expand ? SizeValue.Fill : SizeValue.Hug,
            MinWidth = ButtonStyles.MinWidth,
            Padding = EdgeInsets.Symmetric(padX, 0),
            Background = fill,
            CornerRadius = new CornerRadii(radius),
            BorderWidth = borderWidth,
            BorderColor = borderColor,
        }, content);

        // Pressed (§01/A12): filled variants swap Base→Pressed on the same rrect; Outline/Ghost gain
        // a SurfaceSubtle fill while pressed. (Link's pressed TEXT swap joins with rich text.)
        ColorToken? pressedFill = Variant switch
        {
            Variant.Link => null,
            Variant.Outline or Variant.Ghost => theme.SurfaceSubtle,
            _ => colors.Pressed,
        };

        return new Pressable(container, Disabled ? null : OnPressed)
        {
            Disabled = Disabled,
            Label = Label,
            PressedBackground = Disabled ? null : pressedFill,
        };
    }
}
