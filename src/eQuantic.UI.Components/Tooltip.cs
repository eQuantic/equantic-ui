using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Tooltip (wave 3b): a short hint on an INVERSE pill (TextPrimary fill /
/// TextInverse caption — the Toast pair, flipping correctly in both modes) that shows while the
/// pointer hovers the child. Pure mechanics on both targets: the Anchored hover reveal is CSS-only
/// on web (zero JS, never on touch) and rides the host's hover pipeline on Photon. Keep it to a few
/// words — tooltips are hints, not documentation. Keyboard FOCUS reveals it exactly like hover
/// (:has(:focus-visible), still zero JS), and the panel is a real <c>role="tooltip"</c> the anchor
/// points at with <c>aria-describedby</c> — a screen reader hears the hint after the control's
/// name whether or not anything is visually revealed.
/// v1 fences: show/hide delay, arrow caret, Esc suppression (WCAG 1.4.13's dismissable clause —
/// joins the Anchored Esc work, one controller for every floating panel).
/// </summary>
public sealed class Tooltip : StatelessComponent
{
    public Tooltip(VisualNode child, string text)
    {
        Child = child;
        Text = text;
    }

    public VisualNode Child { get; init; }
    public string Text { get; init; }
    public AnchorPlacement Placement { get; init; } = AnchorPlacement.TopCenter;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var pill = new Box(new BoxStyle
        {
            Background = theme.TextPrimary,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Small)),
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
        }, new Text(Text, TypeRole.Caption, theme.TextInverse, maxLines: 1));

        return new Anchored(Child, pill)
        {
            Placement = Placement,
            OpenOnHover = true,
            DescribesAnchor = true,
        };
    }
}
