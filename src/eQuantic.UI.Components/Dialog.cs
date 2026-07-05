using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>One dialog action (spec C2): safe action = Ghost FIRST, committing = Primary/Destructive.</summary>
public sealed record DialogAction(string Label, Action? OnPressed = null, Variant Variant = Variant.Primary);

/// <summary>
/// The design system's Modal/Dialog (spec C2): an interrupting decision — centered card
/// min(480, screen − 48), Radius.Xl, E5 over the Scrim, Title + BodyM TextSecondary body (8dp gap),
/// right-aligned Medium action buttons (8dp gap, 20dp above). Max 2 actions — a third means an
/// ActionSheet or a screen. DECLARATIVE presence: the app shows it by building it
/// (`if (_confirming) … new Dialog(…)`) and resolves it through the action callbacks;
/// <c>Dismissible</c> (default FALSE — destructive confirms) arms the scrim tap with
/// <c>OnDismiss</c>. v1 fences: enter/exit motion (state-transition system), focus trap and
/// alertdialog announcements (a11y system), stacked modals are a spec violation — don't.
/// </summary>
public sealed class Dialog : StatelessComponent
{
    public Dialog(string title, string body, DialogAction[] actions,
        bool dismissible = false, Action? onDismiss = null)
    {
        if (actions.Length is 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(actions),
                "A Dialog carries 1-2 actions — a third means an ActionSheet or a screen (spec C2).");
        Title = title;
        Body = body;
        Actions = actions;
        Dismissible = dismissible;
        OnDismiss = onDismiss;
    }

    public string Title { get; init; }
    public string Body { get; init; }
    public DialogAction[] Actions { get; init; }

    /// <summary>Scrim tap/back dismiss. FALSE by default: a destructive confirm must be resolved
    /// by a button — the scrim swallows taps without reacting (spec C2).</summary>
    public bool Dismissible { get; init; }

    public Action? OnDismiss { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        var content = new Column(gap: 8);
        content.Add(new Text(Title, TypeRole.Title));
        content.Add(new Text(Body, TypeRole.BodyM, theme.TextSecondary, maxLines: 6));

        var actions = new Row(gap: 8) { Main = MainAlign.End };
        foreach (var action in Actions)
        {
            actions.Add(new Button(action.Label, action.Variant, SizeVariant.Medium, action.OnPressed));
        }

        var body = new Column(gap: 20) { Width = SizeValue.Fill };
        body.Add(content);
        body.Add(actions);

        // The scrim is a full-viewport Pressable UNDER the card: dismissible arms it with
        // OnDismiss; otherwise it stays disabled — swallowing taps without reacting, exactly the
        // native hit-dispatch contract ("disabled regions swallow") and a dead click on web.
        var scrim = new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Scrim,
        }), Dismissible ? OnDismiss : null)
        {
            Disabled = !Dismissible,
            Label = "dismiss",
        };

        // The card: one Box carries ALL the chrome (fill, radius, E5 shadow — the shadow follows
        // the same rrect) around the layout-only column. Gutters keep it at min(480, screen − 48).
        var elevated = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MaxWidth = 480,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(Radius.Xl),
            Elevation = 5,
            Padding = EdgeInsets.All(Space.S5),
        }, body);
        var centering = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.Center,
            Cross = CrossAlign.Center,
            Padding = EdgeInsets.Symmetric(Space.S6, 0),
        };
        centering.Add(elevated);

        var layers = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layers.Add(scrim);
        layers.Add(centering);
        return new Overlay(layers);
    }
}
