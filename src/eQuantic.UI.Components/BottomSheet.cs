using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's BottomSheet (spec C4): a MODAL surface anchored to the bottom edge — top
/// corners at the theme's ExtraLarge shape, E4 over the Scrim, a 32×4 drag handle centered above
/// the content. Dismissible by DEFAULT (sheets are optional context, unlike destructive dialogs):
/// the scrim tap fires <see cref="OnDismiss"/>. DECLARATIVE presence like the Dialog. v1 fences:
/// enter/exit slide (state-transition system), drag-to-dismiss (gesture polish), detents.
/// </summary>
public sealed class BottomSheet : StatelessComponent
{
    public BottomSheet(VisualNode content, Action? onDismiss = null, bool dismissible = true)
    {
        Content = content;
        OnDismiss = onDismiss;
        Dismissible = dismissible;
    }

    public VisualNode Content { get; init; }
    public Action? OnDismiss { get; init; }
    public bool Dismissible { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

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

        var body = new Column(gap: Space.S3) { Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        // The drag handle — an affordance today, the drag-to-dismiss grip when gestures land.
        var handleRow = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.Center };
        handleRow.Add(new Box(new BoxStyle
        {
            Width = 32, Height = 4,
            Background = theme.BorderStrong,
            CornerRadius = new CornerRadii(Radius.Full),
        }));
        body.Add(handleRow);
        body.Add(Content);

        var radius = theme.Shape(ShapeScale.ExtraLarge);
        var sheet = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(radius, radius, 0, 0),
            Elevation = 4,
            Padding = new EdgeInsets(Space.S5, Space.S3, Space.S5, Space.S6),
        }, body);

        // ASKED TO DRAW IN THE FLOW: the sheet is the sheet; the scrim, the viewport-tall anchor
        // that pins it to the bottom, the portal and the Escape binding are the LAYER (see InFlow).
        // The drag-to-dismiss goes with them — there is nothing to dismiss it from.
        if (context.InFlow) return sheet;

        var anchor = new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.End,
        };
        // Enter motion (spec §06): the sheet RISES while the scrim fades — two presences in one
        // layer, because sliding the full-viewport scrim would expose a gap at the top. A
        // dismissible sheet is also DRAGGABLE (gestures v2): pull it down past the threshold and
        // OnDismiss fires — the exit motion then completes from wherever the drag left it.
        VisualNode sheetNode = Dismissible ? new DragDismiss(sheet, OnDismiss) : sheet;
        anchor.Add(new Presence(sheetNode, PresenceMotion.SlideUp));

        var layers = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layers.Add(new Presence(scrim));
        layers.Add(anchor);
        // Escape closes it, the way every dialog on every platform does. Declared as an ordinary
        // Shortcut rather than taught to each realizer: the node already exists on web and native,
        // and its "last registered wins" dispatch IS the stacking rule — a sheet opened over a
        // dialog takes the key, and closes first.
        var layer = new Overlay(layers);
        return Dismissible && OnDismiss is { } escape ? new Shortcut(layer, KeyChord.Escape, escape) : layer;
    }
}
