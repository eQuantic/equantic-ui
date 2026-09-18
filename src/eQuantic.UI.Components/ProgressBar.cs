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

    /// <summary>
    /// 8dp meter styling (goal/quota) instead of the 4dp default.
    /// <para>
    /// Backed by a FIELD so <see cref="AdoptConfig"/> can copy it. This component is retained across
    /// the app's rebuilds, and <c>UiComponent.AdoptConfig</c>'s contract is to copy the fresh
    /// CONFIGURATION — constructor AND init props. An `init` accessor cannot be written from there,
    /// so a plain auto-property would silently keep the first parent build's value forever: the bar
    /// would go on announcing a name the screen has since changed.
    /// </para>
    /// </summary>
    public bool Prominent { get => _prominent; init => _prominent = value; }

    /// <summary>
    /// What the progress is FOR, announced by assistive tech — "Uploading", "Storage used". Spec
    /// B14 asks for role=progressbar, and a role with no name announces "progress bar" and nothing
    /// about which one, on a screen that may hold several.
    /// </summary>
    public string Label { get => _label; init => _label = value; }

    /// <summary>
    /// The progress IN WORDS, when the ratio is not what a person would say — "3 of 7 files",
    /// "2 minutes left". Null announces the number against its range, which a reader renders as a
    /// percentage by itself and is right for a bare ratio.
    /// </summary>
    public string? ValueText { get => _valueText; init => _valueText = value; }

    private bool _prominent;
    private string _label = "";
    private string? _valueText;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not ProgressBar fresh) return;
        // Forward-only (spec B14): a lower incoming value marks the NEXT build to snap.
        _snapNext = fresh.Value is { } incoming && Value is { } current && incoming < current;
        Value = fresh.Value;
        Variant = fresh.Variant;
        // The rest of the configuration too, or a parent that renames the bar keeps announcing the
        // old name: this instance is RETAINED, so what Build reads is whatever was adopted here.
        _label = fresh.Label;
        _valueText = fresh.ValueText;
        _prominent = fresh.Prominent;
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
                CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            };
            if (filledWeight > 0)
            {
                track.Add(new Flexible(new Box(new BoxStyle
                {
                    Height = height,
                    Background = theme.Colors(Variant).Base,
                    CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
                }), filledWeight)
                {
                    AnimateChanges = animate,
                });
            }
            if (filledWeight < 1000)
            {
                // B14: the counterweight animates WITH the fill — constant denominator, so the
                // visible ratio glides instead of jumping when only one side moved.
                track.Add(new Spacer(1000 - filledWeight) { AnimateChanges = animate });
            }
            // Spec B14: role=progressbar with the value it holds — the value the bar IS DRAWN FROM,
            // not the caller's, because an announcement that disagreed with the pixels would
            // describe a different control. That is `filledWeight`, NOT `clamped`: the weights
            // quantize to a thousandth, so 0.4504 paints 450/550 — the bar is at 0.45 and saying
            // 0.4504 is the same defect one decimal place further down. The range is 0..1 by this
            // component's own definition of Value, so it is stated rather than guessed at.
            return new Progress(track)
            {
                Label = Label,
                Value = new RangeValue(filledWeight / 1000f, 0, 1) { Text = ValueText },
            };
        }

        // Indeterminate: a full-width layer holds the 30% segment by flex weight; LoopMotion sweeps
        // the LAYER (own-width fractions == track fractions), and the track Box clips the overflow.
        var segment = new Row(gap: 0) { Width = SizeValue.Fill, Height = height };
        segment.Add(new Flexible(new Box(new BoxStyle
        {
            Height = height,
            Background = theme.Colors(Variant).Base,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
        }), 300));
        segment.Add(new Spacer(700));

        // Indeterminate keeps the ROLE and carries no value — ARIA's own rule, and the honest one:
        // the bar is saying that something is happening, which is all it knows.
        return new Progress(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = height,
            Background = theme.SurfaceSubtle,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            Clip = true,
        }, new LoopMotion(segment, LoopEffect.SlideX, SweepFromX, SweepToX, SweepDurationMs)))
        {
            Label = Label,
        };
    }
}
