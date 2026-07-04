using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// NATIVE REALIZER for the Button: emits a resolved (target-neutral) <see cref="ButtonStyle"/> as
/// Photon draw commands — the container chrome the component layer (and the golden suite) drives:
/// focus double-ring, fill, border. Label/icon runs land with the text stack (plan W4). 1–2 draws at
/// rest, exactly as the spec's render budget states. The web realizer lowers the same struct to
/// DOM + CSS (docs/SHARED-COMPONENTS-PLAN.md).
/// </summary>
public static class ButtonRenderer
{
    public static void Draw(DisplayListBuilder builder, Rect bounds, in ButtonStyle style)
    {
        var shape = new RRect(bounds, new CornerRadii(style.CornerRadius));

        // Focus ring (spec §01): 2dp gap ring in Surface, then the 2dp Focus ring — two centered
        // strokes just outside the container (outer edge lands at bounds + 4dp, radius + 4).
        if (style.ShowFocusRing)
        {
            builder.StrokeRRect(
                new RRect(bounds.Inflate(1), new CornerRadii(style.CornerRadius + 1)), 2,
                Paint.Solid(Apply(style.FocusRingGapColor, style.Opacity)));
            builder.StrokeRRect(
                new RRect(bounds.Inflate(3), new CornerRadii(style.CornerRadius + 3)), 2,
                Paint.Solid(Apply(style.FocusRingColor, style.Opacity)));
        }

        if (style.Fill.A > 0)
            builder.FillRRect(shape, Paint.Solid(Apply(style.Fill, style.Opacity)));

        if (style.BorderWidth > 0 && style.BorderColor.A > 0)
        {
            // Borders draw INSIDE the bounds (spec fence): centered stroke on the half-width-deflated
            // shape covers exactly [0, w] inward.
            builder.StrokeRRect(
                new RRect(bounds.Inflate(-style.BorderWidth / 2), new CornerRadii(style.CornerRadius).Deflate(style.BorderWidth / 2)),
                style.BorderWidth,
                Paint.Solid(Apply(style.BorderColor, style.Opacity)));
        }
    }

    // Disabled is a 38% opacity GROUP in the spec; until the engine grows saveLayer opacity groups,
    // per-color alpha is exact for the button's non-overlapping chrome (fill-only or border-only).
    private static Color Apply(Color color, float opacity) =>
        opacity >= 1f ? color : color.WithOpacity(opacity);
}
