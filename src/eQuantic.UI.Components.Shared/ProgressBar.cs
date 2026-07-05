using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components.Shared;

/// <summary>
/// The design system's ProgressBar (spec B14): SurfaceSubtle track, Primary fill (status tints
/// allowed for meters), Radius.Full on both rrects, heights 4 (default) / 8 (prominent), fills the
/// row width. The fill fraction realizes as flex weights (round(value·1000) vs the remainder) — the
/// same leftover-by-weight math on both realizers, no percent sizing needed. v1 fence: determinate
/// only (the indeterminate 1.2s loop joins with the animation system); value changes snap.
/// </summary>
public sealed class ProgressBar : StatelessComponent
{
    public ProgressBar(float value, Variant variant = Variant.Primary)
    {
        Value = value;
        Variant = variant;
    }

    /// <summary>Progress 0..1 (clamped).</summary>
    public float Value { get; init; }

    public Variant Variant { get; init; }

    /// <summary>8dp meter styling (goal/quota) instead of the 4dp default.</summary>
    public bool Prominent { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var height = Prominent ? 8f : 4f;
        var clamped = Math.Clamp(Value, 0f, 1f);
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
            }), filledWeight));
        }
        if (filledWeight < 1000)
        {
            track.Add(new Spacer(1000 - filledWeight));
        }
        return track;
    }
}
