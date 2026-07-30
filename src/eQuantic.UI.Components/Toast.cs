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

        var row = new Row(gap: Space.S3) { Cross = CrossAlign.Center };
        // The status dot: the one splash of role color on the inverse pill.
        row.Add(new Box(new BoxStyle
        {
            Width = 8, Height = 8,
            Background = theme.Colors(Status).Base,
            CornerRadius = new CornerRadii(Radius.Full),
        }));
        row.Add(new Text(Message, TypeRole.BodyM, theme.TextInverse, maxLines: 2));
        if (ActionLabel is { } label)
        {
            row.Add(new Pressable(new Box(new BoxStyle
            {
                Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            }, new Text(label, TypeRole.Label, theme.TextInverse)), OnAction)
            {
                Label = label,
            });
        }

        var pill = new Box(new BoxStyle
        {
            Background = theme.TextPrimary,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Elevation = 3,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S3),
            MaxWidth = 480,
        }, row);

        var anchor = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.End,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S6),
        };
        // Enter motion (spec §06): the pill rises into place (SlideUp) — the toast entrance.
        anchor.Add(new Presence(pill, PresenceMotion.SlideUp));

        return new Overlay(anchor) { Modal = false };
    }
}
