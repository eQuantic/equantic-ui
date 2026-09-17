namespace eQuantic.UI.Primitives;

/// <summary>
/// A two-stop ELLIPTICAL radial fill — the "spotlight" glow marketing heroes wash behind their
/// content. Closed shape like <see cref="GridPattern"/>: two token stops, an elliptical extent, a
/// center expressed as FRACTIONS of the box (the CSS percentage form), which keeps it
/// resolution-independent and realizable on both targets. Web lowers to <c>radial-gradient()</c>;
/// native runs <c>Paint.Radial</c>, whose normalized-elliptical-distance math the shader mirrors.
/// </summary>
/// <param name="From">Color at the center.</param>
/// <param name="To">Color reached at the ellipse boundary (usually the transparent twin of From).</param>
/// <param name="CenterX">Center as a fraction of the box WIDTH (0 = left, 1 = right).</param>
/// <param name="CenterY">Center as a fraction of the box HEIGHT (0 = top, 1 = bottom).</param>
/// <param name="RadiusX">Horizontal extent in dp.</param>
/// <param name="RadiusY">Vertical extent in dp.</param>
public readonly record struct RadialGradient(
    ColorToken From,
    ColorToken To,
    float CenterX,
    float CenterY,
    float RadiusX,
    float RadiusY);
