using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>Which edge a <see cref="Drawer"/> slides from (logical: Start mirrors in RTL).</summary>
public enum DrawerEdge : byte
{
    Start = 0,
    End = 1,
}

/// <summary>
/// The design system's side Drawer (wave 3b): a full-height E3 Surface panel anchored to the
/// start/end edge over a dismissible scrim — navigation, filters, detail panes. Fully CONTROLLED
/// (the caller owns <see cref="Open"/>; scrim tap fires <see cref="OnDismiss"/>), declarative like
/// Dialog/BottomSheet: build it open, remove it closed. The panel wraps in <see cref="Presence"/>
/// so it fades in through the state-transition system.
/// v1 fences: lateral slide motion (PresenceMotion gains SlideStart with the motion pack),
/// drag-to-close (the DragDismiss horizontal axis), focus trap (a11y system).
/// </summary>
public sealed class Drawer : StatelessComponent
{
    public Drawer(VisualNode content, bool open, Action? onDismiss = null)
    {
        Content = content;
        Open = open;
        OnDismiss = onDismiss;
    }

    public VisualNode Content { get; init; }
    public bool Open { get; init; }
    public Action? OnDismiss { get; init; }
    public DrawerEdge Edge { get; init; } = DrawerEdge.Start;

    /// <summary>Panel width in dp (spec: 320 fits a nav list with 48dp rows).</summary>
    public float Width { get; init; } = 320;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        if (!Open) return new Box();

        var layer = new Stack();
        layer.Add(new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Scrim,
        }), OnDismiss) { Label = "Dismiss" });

        var panel = new Box(new BoxStyle
        {
            Width = Width,
            Height = SizeValue.Fill,
            Background = theme.Surface,
            Elevation = 3,
            Padding = EdgeInsets.All(Space.S4),
        }, Content);

        layer.Add(Edge == DrawerEdge.Start
            ? new Positioned(new Presence(panel), top: 0, bottom: 0, start: 0)
            : new Positioned(new Presence(panel), top: 0, bottom: 0, end: 0));

        // Escape closes it, the way every dialog on every platform does. Declared as an ordinary
        // Shortcut rather than taught to each realizer: the node already exists on web and native,
        // and its "last registered wins" dispatch IS the stacking rule — a sheet opened over a
        // dialog takes the key, and closes first.
        var overlay = new Overlay(layer);
        return OnDismiss is { } escape ? new Shortcut(overlay, KeyChord.Escape, escape) : overlay;
    }
}
