using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Switch (spec B12), CONTROLLED: track 52×32 Radius.Full (off = BorderStrong
/// fill, on = Primary), thumb 26dp white with a 3dp inset, realized through Stack + Positioned.
/// The effect is immediate — a Switch never needs a Save button. v1 fences: the slide/crossfade
/// motion, the draggable thumb (spring settle) and the press-stretch join the animation/gesture
/// systems; the press-stretch joins gestures.
/// </summary>
public sealed class Switch : StatelessComponent
{
    public Switch(bool on, Action? onChanged = null)
    {
        On = on;
        OnChanged = onChanged;
    }

    public bool On { get; init; }
    public Action? OnChanged { get; init; }
    public bool Disabled { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var trackFill = On ? theme.Colors(Variant.Primary).Base : theme.BorderStrong;
        if (Disabled) trackFill = trackFill.WithOpacity(theme.DisabledOpacity);

        var track = new Box(new BoxStyle
        {
            Width = 52,
            Height = 32,
            Background = trackFill,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        });

        var thumb = new Box(new BoxStyle
        {
            Width = 26,
            Height = 26,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Elevation = 1,
        });

        var stack = new Stack();
        stack.Add(track);
        stack.Add(On
            ? new Positioned(thumb, top: 3, end: 3)
            : new Positioned(thumb, top: 3, start: 3));

        return new Pressable(stack, Disabled ? null : OnChanged)
        {
            Disabled = Disabled,
            Label = On ? "On" : "Off",
        };
    }
}
