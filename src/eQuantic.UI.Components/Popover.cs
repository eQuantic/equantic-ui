using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Popover (wave 3): arbitrary anchored content on an E2 Surface card —
/// the generic sibling of <see cref="Menu"/> for rich panels (filters, pickers, share sheets).
/// Fully CONTROLLED: the caller owns <see cref="Open"/> (its trigger already toggles state);
/// outside taps fire <see cref="OnDismiss"/>. The panel carries Radius.Md, 1dp Border, S3 padding;
/// the caller composes everything inside from the ordinary vocabulary.
/// v1 fences: arrow/caret pointing at the anchor. NOT focus-trapped by design — a popover is a
/// non-modal surface (WAI-ARIA), so Tab walks out of it; the modal trap belongs to Dialog,
/// BottomSheet and Drawer (§10). Its own Escape-to-close joins the Anchored dismiss path.
/// </summary>
public sealed class Popover : StatelessComponent
{
    public Popover(VisualNode trigger, VisualNode content, bool open, Action? onDismiss = null)
    {
        Trigger = trigger;
        Content = content;
        Open = open;
        OnDismiss = onDismiss;
    }

    public VisualNode Trigger { get; init; }
    public VisualNode Content { get; init; }
    public bool Open { get; init; }
    public Action? OnDismiss { get; init; }
    public AnchorPlacement Placement { get; init; } = AnchorPlacement.BottomStart;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var panel = new Box(new BoxStyle
        {
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = 1,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.All(Space.S3),
        }, Content);

        // ASKED TO DRAW IN THE FLOW: the panel is the popover; the ANCHORING to a trigger — and
        // the trigger itself — is how it is normally presented, not what it is (see InFlow).
        if (context.InFlow) return panel;

        return new Anchored(Trigger, panel)
        {
            Placement = Placement,
            Open = Open,
            OnDismiss = OnDismiss,
        };
    }
}
