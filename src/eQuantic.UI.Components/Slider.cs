using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A value picked from a CONTINUOUS range (spec B13) — brightness, a budget, a threshold — where the
/// exact number matters less than where it sits between the ends. When the number is what the user
/// is really after, a <see cref="Stepper"/> or a typed field is the better control. CONTROLLED: the
/// caller owns <see cref="Value"/>.
/// <para>
/// The track is filled up to the value, so the proportion reads without decoding a thumb position,
/// and the ends are labelled by the caller rather than by tick text nobody can hit. Pressing the
/// track moves ONE step toward the press — the scrollbar's page-click, which is what a track press
/// means everywhere else — and dragging anywhere on it SCRUBS continuously, relative to where the
/// value already is, so grabbing the thumb never makes it jump out from under the finger.
/// </para>
/// </summary>
public sealed class Slider : StatelessComponent
{
    /// <summary>Track thickness; the thumb is the target, the track is the readout.</summary>
    private const float TrackHeight = 4;
    private const float ThumbSize = 20;

    public Slider(float value, Action<float>? onChanged = null)
    {
        Value = value;
        OnChanged = onChanged;
    }

    public float Value { get; init; }
    public Action<float>? OnChanged { get; init; }

    public float Min { get; init; }
    public float Max { get; init; } = 1;

    /// <summary>Granularity of one track press. 0 = a tenth of the range.</summary>
    public float Step { get; init; }

    public bool Disabled { get; init; }
    public Variant Variant { get; init; } = Variant.Primary;

    /// <summary>Announced by the control — "Brightness", not "0.4".</summary>
    public string Label { get; init; } = "";

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var span = Max - Min;
        var fraction = span <= 0 ? 0f : Math.Clamp((Value - Min) / span, 0f, 1f);
        var step = Step > 0 ? Step : span / 10f;
        var accent = theme.Colors(Variant).Base;
        var fill = Disabled ? theme.BorderStrong : accent;

        var thumb = new Box(new BoxStyle
        {
            Width = ThumbSize,
            Height = ThumbSize,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = 2,
            BorderColor = fill,
            Elevation = 2,
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        });

        // Proportional layout WITHOUT knowing the pixel width: the two track halves are flex weights.
        // A zero weight would collapse the side entirely, so each end keeps a hairline of presence.
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Center };

        row.Add(new Flexible(TrackHalf(fill, filled: true, enabled: !Disabled,
            onPressed: () => OnChanged?.Invoke(Math.Max(Min, Value - step))), Weight(fraction)));
        row.Add(thumb);
        row.Add(new Flexible(TrackHalf(theme.BorderStrong, filled: false, enabled: !Disabled,
            onPressed: () => OnChanged?.Invoke(Math.Min(Max, Value + step))), Weight(1 - fraction)));

        // The gesture is NORMALIZED because the track is fluid: the component cannot know its pixel
        // width and the host can, so it reports a fraction. It does not FOLLOW, either — the value
        // places the thumb through the flex weights above, and translating it as well would move it
        // twice.
        VisualNode surface = Disabled ? row : new Draggable(row)
        {
            Axis = DragAxis.Horizontal,
            Normalized = true,
            Follows = false,
            Min = 0,
            Max = 1,
            RestOffset = fraction,
            OnMoved = f => OnChanged?.Invoke(Quantize(Min + f * span, step)),
        };

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Touch.MinTarget,
            Opacity = Disabled ? theme.DisabledOpacity : 1f,
        }, surface);
    }

    /// <summary>A scrub lands on the same values a press does — a stepped slider has no in-between
    /// positions, however finely the finger moves.</summary>
    private float Quantize(float value, float step) =>
        Math.Clamp(Step > 0 ? Min + MathF.Round((value - Min) / step) * step : value, Min, Max);

    /// <summary>
    /// Flex weights are integers, so the fraction becomes per-mille — 0.1% granularity, finer than
    /// any pointer. A collapsed side still has to exist, or the thumb would sit off the track.
    /// </summary>
    private static int Weight(float fraction) => Math.Max(1, (int)MathF.Round(fraction * 1000));

    /// <summary>
    /// One side of the track: a hairline bar inside a full-height press target, so the press area is
    /// <see cref="Touch.MinTarget"/> tall while the visible track stays 4dp.
    /// </summary>
    private static VisualNode TrackHalf(ColorToken color, bool filled, bool enabled, Action onPressed)
    {
        var bar = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = TrackHeight,
            Background = color,
            // Only the outer end is rounded — the inner end butts against the thumb.
            CornerRadius = filled
                ? new CornerRadii(TrackHeight / 2, 0, 0, TrackHeight / 2)
                : new CornerRadii(0, TrackHeight / 2, TrackHeight / 2, 0),
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        });

        var centered = new Column(gap: 0) { Height = SizeValue.Fill, Main = MainAlign.Center };
        centered.Add(bar);

        var target = new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, centered);
        return enabled
            ? new Pressable(target, onPressed) { Label = filled ? "Decrease" : "Increase" }
            : target;
    }
}
