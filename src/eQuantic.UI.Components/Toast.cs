using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Toast/Snackbar (spec C3): a TRANSIENT, NON-MODAL notice anchored to the
/// bottom — an inverse-surface pill (TextPrimary fill / TextInverse label, the pair that flips
/// correctly in both modes), E3, Radius.Full, with an optional single action. DECLARATIVE presence
/// like the Dialog: the app shows it by building it and hides it by not building it — the
/// auto-dismiss TIMER is the app's (or the host clock's) concern, not the component's. The layer is
/// non-modal: everything behind stays clickable; only the pill (and its action) takes input.
/// v1 fences: enter/exit motion (state-transition system) and toast queueing (one at a time).
/// </summary>
public sealed class Toast : StatelessComponent
{
    public Toast(string message, Variant status = Variant.Info,
        string? actionLabel = null, Action? onAction = null)
    {
        Message = message;
        Status = status;
        ActionLabel = actionLabel;
        OnAction = onAction;
    }

    public string Message { get; init; }
    public Variant Status { get; init; }
    public string? ActionLabel { get; init; }
    public Action? OnAction { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        // The notices' slot (docs/ELEMENT-TREE-PLAN.md §3.10): each value the theme names, or today's.
        var slot = theme.Notice;
        var text = slot?.ToastText ?? theme.TextInverse;

        var row = new Row(gap: slot?.ToastGap ?? Space.S3) { Cross = CrossAlign.Center };
        // The status dot: the one splash of role color on the inverse pill.
        row.Add(new Box(new BoxStyle
        {
            Width = slot?.ToastDot ?? 8, Height = slot?.ToastDot ?? 8,
            Background = theme.Colors(Status).Base,
            CornerRadius = new CornerRadii(Radius.Full),
        }));
        row.Add(new Text(Message, TypeRole.BodyM, text, maxLines: 2));
        if (ActionLabel is { } label)
        {
            row.Add(new Pressable(new Box(new BoxStyle
            {
                Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            }, new Text(label, TypeRole.Label, text)), OnAction)
            {
                Label = label,
            });
        }

        var pill = new Box(new BoxStyle
        {
            Background = slot?.ToastFill ?? theme.TextPrimary,
            CornerRadius = new CornerRadii(theme.Shape(slot?.ToastShape ?? ShapeScale.Full)),
            Elevation = slot?.ToastElevation ?? 3,
            Padding = slot?.ToastPadding ?? EdgeInsets.Symmetric(Space.S4, Space.S3),
            MaxWidth = slot?.ToastMaxWidth ?? 480,
        }, row);

        var anchor = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.End,
            Cross = CrossAlign.Center,
            Padding = slot?.ToastMargin ?? EdgeInsets.Symmetric(Space.S4, Space.S6),
        };
        // Spec C4: a POLITE live region — the toast tells the user what just happened and must not
        // cut off whatever a reader is saying. It wraps the PILL rather than the layer: the layer
        // fills the viewport, and a live region that size would hand a reader the whole screen as
        // the announcement. The Overlay below cannot carry this itself — both realizers strip every
        // semantic from a non-modal layer, which is exactly why this row stayed open.
        var announced = new LiveRegion(pill);

        // Enter motion (spec §06): the pill rises into place (SlideUp) — the toast entrance.
        anchor.Add(new Presence(announced, PresenceMotion.SlideUp));

        return new Overlay(anchor) { Modal = false };
    }
}
