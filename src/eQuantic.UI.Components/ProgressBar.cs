using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's ProgressBar (spec B14): SurfaceSubtle track, Primary fill (status tints
/// allowed for meters), Radius.Full on both rrects, heights 4 (default) / 8 (prominent), fills the
/// row width. Determinate fill realizes as flex weights (round(value·1000) vs the remainder) — the
/// same leftover-by-weight math on both realizers, no percent sizing needed. <c>null</c> value =
/// INDETERMINATE (spec: 30% segment sweeping -35%..105% of the track on a 1.2s loop, transform-only
/// — LoopMotion inside a clipping track). STATEFUL for the B14 value-transition contract: the
/// positional reconciler retains the instance across the app's controlled re-renders, and
/// <see cref="AdoptConfig"/> compares each fresh value against the current one — forward changes
/// animate Base 200ms standard (web: a generated flex-grow transition; native joins with the
/// transition animator), REGRESSIONS SNAP (honesty over smoothness, spec B14).
/// </summary>
public sealed class ProgressBar : StatefulComponent
{
    /// <summary>Indeterminate sweep geometry/clock (spec B14): the 30%-of-track segment sweeps fully
    /// across — off-screen left (-35%) to off-screen right (105%) — every 1.2s.</summary>
    private const float SweepFromX = -0.35f;
    private const float SweepToX = 1.05f;
    private const int SweepDurationMs = 1200;

    private bool _snapNext;

    public ProgressBar(float? value = null, Variant variant = Variant.Primary)
    {
        Value = value;
        Variant = variant;
    }

    /// <summary>Progress 0..1 (clamped); <c>null</c> = indeterminate.</summary>
    public float? Value { get; private set; }

    public Variant Variant { get; private set; }

    /// <summary>8dp meter styling (goal/quota) instead of the 4dp default.</summary>
    public bool Prominent { get; init; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not ProgressBar fresh) return;
        // Forward-only (spec B14): a lower incoming value marks the NEXT build to snap.
        _snapNext = fresh.Value is { } incoming && Value is { } current && incoming < current;
        Value = fresh.Value;
        Variant = fresh.Variant;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var height = Prominent ? 8f : 4f;

        if (Value is { } value)
        {
            var animate = !_snapNext;
            _snapNext = false;
            var clamped = Math.Clamp(value, 0f, 1f);
            var filledWeight = (int)MathF.Round(clamped * 1000);

            var track = new Row(gap: 0)
            {
                Width = SizeValue.Fill,
                Height = height,
                Background = theme.SurfaceSubtle,
                CornerRadius = new CornerRadii(Radius.Full),
            };
            if (filledWeight > 0)
            {
                track.Add(new Flexible(new Box(new BoxStyle
                {
                    Height = height,
                    Background = theme.Colors(Variant).Base,
                    CornerRadius = new CornerRadii(Radius.Full),
                }), filledWeight)
                {
                    AnimateChanges = animate,
                });
            }
            if (filledWeight < 1000)
            {
                track.Add(new Spacer(1000 - filledWeight));
            }
            return track;
        }

        // Indeterminate: a full-width layer holds the 30% segment by flex weight; LoopMotion sweeps
        // the LAYER (own-width fractions == track fractions), and the track Box clips the overflow.
        var segment = new Row(gap: 0) { Width = SizeValue.Fill, Height = height };
        segment.Add(new Flexible(new Box(new BoxStyle
        {
            Height = height,
            Background = theme.Colors(Variant).Base,
            CornerRadius = new CornerRadii(Radius.Full),
        }), 300));
        segment.Add(new Spacer(700));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = height,
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Full),
            Clip = true,
        }, new LoopMotion(segment, LoopEffect.SlideX, SweepFromX, SweepToX, SweepDurationMs));
    }
}
