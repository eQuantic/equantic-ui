using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Switch (spec B12), CONTROLLED: track 52×32 Radius.Full (off = BorderStrong
/// fill, on = Primary), thumb 26dp white with a 3dp inset, realized through Stack + Positioned.
/// The effect is immediate — a Switch never needs a Save button. The thumb DRAGS as well as taps:
/// a switch that only takes taps feels dead under a thumb that is already sliding. The limits are
/// signed by state — off travels forward, on travels back — so the reported offset is always the
/// distance from where the thumb is sitting, and a drag that ends where it began changes nothing.
/// Fence: the slide/crossfade motion between the two ends joins the animation system.
/// </summary>
public sealed class Switch : StatelessComponent
{
    public Switch(bool on, Action? onChanged = null)
    {
        On = on;
        OnChanged = onChanged;
    }

    /// <summary>Track 52, thumb 26, inset 3 each side — what is left is what the thumb can cross.</summary>

    public bool On { get; init; }
    public Action? OnChanged { get; init; }
    public bool Disabled { get; init; }

    /// <summary>What this switch is FOR — "Dark mode", "Notifications" — which is the accessible
    /// name. The on/off STATE is announced by the role's own attribute, so it never goes here.
    /// Null is acceptable when a visible Text beside the switch labels it for sighted users too,
    /// though pointing both at the same words is better.</summary>
    public string? Label { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var trackFill = On ? theme.Colors(Variant.Primary).Base : theme.BorderStrong;
        if (Disabled) trackFill = trackFill.WithOpacity(theme.DisabledOpacity);

        // The ladder comes from the TOKEN LAYER, resolved for this target's density — a switch is
        // 52×32 under a thumb and 44×26 under a pointer, and neither number is written here.
        var density = context.Density;
        var travel = Sizing.SwitchTravel(density);
        var inset = Sizing.SwitchInset;

        var track = new Box(new BoxStyle
        {
            Width = Sizing.SwitchWidth(density),
            Height = Sizing.SwitchHeight(density),
            Background = trackFill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        });

        var thumb = new Box(new BoxStyle
        {
            Width = Sizing.SwitchThumb(density),
            Height = Sizing.SwitchThumb(density),
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Elevation = 1,
        });

        // The thumb travels the track's slack: the width, less the thumb, less both insets.
        VisualNode draggableThumb = Disabled ? thumb : new Draggable(thumb, offset => Release(offset, travel))
        {
            Axis = DragAxis.Horizontal,
            // Signed by state, so the thumb never drags off the end it is already resting against.
            Min = On ? -travel : 0,
            Max = On ? 0 : travel,
        };

        var stack = new Stack();
        stack.Add(track);
        stack.Add(On
            ? new Positioned(draggableThumb, top: inset, end: inset)
            : new Positioned(draggableThumb, top: inset, start: inset));

        // The STATE goes in aria-checked (role: switch), never in the name — "On, switch" said the
        // same thing twice, and the name changing with the state read as a different control. What
        // remains for the name is what the switch IS FOR, which only the caller knows.
        return new Pressable(stack, Disabled ? null : OnChanged)
        {
            Disabled = Disabled,
            Role = PressableRole.Switch,
            Selected = On,
            Label = Label,
        };
    }

    /// <summary>Past half the slack the switch flips; short of it the thumb returns and nothing
    /// changed — which is why the caller hears about a CHANGE, not about every release.</summary>
    private void Release(float offset, float travel)
    {
        var next = On ? offset > -travel / 2 : offset >= travel / 2;
        if (next != On) OnChanged?.Invoke();
    }
}
