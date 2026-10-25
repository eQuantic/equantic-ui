using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// CSS formatting for the shared token types — used by BOTH the stylesheet generator and the
/// realizer's inline styles, so there is exactly one way a token becomes CSS.
/// </summary>
public static class TokenCss
{
    /// <summary>#RRGGBB (or #RRGGBBAA when translucent), lowercase.</summary>
    public static string Hex(Color color) => color.A == 255
        ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
        : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    /// <summary>
    /// A <see cref="ColorToken"/> as a single CSS value: <c>light-dark(#light, #dark)</c> — the modern
    /// CSS color function that theme-switches with <c>color-scheme</c>, keeping lowered HTML mode-free
    /// exactly like the abstract tree. Collapses to a plain hex when both modes match.
    /// </summary>
    public static string Value(ColorToken token) => token.Light == token.Dark
        ? Hex(token.Light)
        : $"light-dark({Hex(token.Light)}, {Hex(token.Dark)})";

    /// <summary>
    /// The PROPORTIONAL stack, behind <c>--eq-font-family</c>: the theme's text face
    /// (<see cref="IAppTheme.TextFamily"/>), which the theme's stylesheet declares on <c>:root</c> when
    /// the theme names one, in front of the platform's own. The document and every role with no face
    /// of its own are set in it.
    /// <para>
    /// CHARACTER-IDENTICAL to the runtime's SANS_STACK (lowering.ts), for the reason
    /// <see cref="MonoStack"/> gives.
    /// </para>
    /// </summary>
    public const string SansStack = "var(--eq-font-family, " + SansFallback + ")";

    /// <summary>The stack behind <c>--eq-font-family</c> — named once so the variable's declaration
    /// and its fallback cannot drift apart.</summary>
    public const string SansFallback = "system-ui, -apple-system, sans-serif";

    /// <summary>The stack behind <c>--eq-font-mono</c> — named once so the variable's declaration
    /// and its fallback cannot drift apart.</summary>
    public const string MonoFallback =
        "ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace";

    /// <summary>
    /// The monospace stack (Text.Mono / rich runs), behind <c>--eq-font-mono</c>: the theme's code
    /// face (<see cref="IAppTheme.MonoFamily"/>) when it names one, in front of ui-monospace, so each
    /// OS otherwise uses its native mono face.
    /// <para>
    /// A mono run lands on an atomic class, and an atomic class beats a <c>body</c> rule, so a face
    /// for code reaches it through the variable: the class name is a content hash that moves between
    /// builds, which is no hook at all.
    /// </para>
    /// <para>
    /// This string must stay CHARACTER-IDENTICAL to the runtime's MONO_STACK (lowering.ts): the
    /// atomic class name is a hash of the declaration, so a server that emits one spelling and a
    /// client that emits another mint two classes for one style and the page re-paints on
    /// hydration.
    /// </para>
    /// </summary>
    public const string MonoStack = "var(--eq-font-mono, " + MonoFallback + ")";

    /// <summary>
    /// A NAMED face in front of the stack that would have been used anyway. The fallback is the
    /// point: a browser asked for a family it does not have silently draws the next one, exactly as
    /// CoreText does — but here we get to say what the next one is, so an unavailable brand face
    /// lands on the system stack rather than on whatever the browser felt like.
    /// <para>
    /// Quoted always. A family with a space in it is the common case ("IBM Plex Sans"), and an
    /// unquoted one is a CSS parse error that takes the whole declaration with it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The family must already have passed <see cref="FaceName.IsWellFormed"/>: this QUOTES, it
    /// does not sanitise, and a family that reached here unchecked could close the style element.
    /// The quoting stays because quoting is what CSS needs, not because it is a defence.
    /// </remarks>
    public static string Face(string family, bool mono) =>
        $"\"{family}\", " + (mono ? MonoStack : SansStack);

    /// <summary>dp → px (1:1 on web; density is the platform's concern).</summary>
    public static string Px(float dp) => dp switch
    {
        0 => "0",
        _ => $"{dp.ToString("0.##", CultureInfo.InvariantCulture)}px",
    };

    public static string Radius(CornerRadii radii) => radii.TopLeft == radii.TopRight
        && radii.TopRight == radii.BottomRight && radii.BottomRight == radii.BottomLeft
        ? Px(radii.TopLeft)
        // CSS order: top-left, top-right, bottom-right, bottom-left — same as the token.
        : $"{Px(radii.TopLeft)} {Px(radii.TopRight)} {Px(radii.BottomRight)} {Px(radii.BottomLeft)}";

    /// <summary>EdgeInsets → CSS padding shorthand (top right bottom left; Start→left in LTR v1).</summary>
    public static string Padding(EdgeInsets insets) =>
        $"{Px(insets.Top)} {Px(insets.End)} {Px(insets.Bottom)} {Px(insets.Start)}";

    /// <summary>
    /// The focus ring's place in a box's shadow list (#508). A transparent zero shadow until the
    /// control the box is the surface of takes keyboard focus, when the generated stylesheet sets
    /// <c>--eq-ring</c> to the handoff's double ring: the ring is drawn INSIDE the list, beside the
    /// box's elevation and glow, where a rule of its own replaced them. The TypeScript twin is
    /// <c>RING_SLOT</c>.
    /// </summary>
    internal const string RingSlot = "var(--eq-ring, 0 0 #0000)";

    /// <summary>An elevation level as a box-shadow value (offset/blur/spread + light-dark color).</summary>
    public static string Shadow(ShadowSpec spec) => spec.IsNone
        ? "none"
        : $"0 {Px(spec.OffsetY)} {Px(spec.Blur)} {Px(spec.Spread)} {Value(spec.Color)}";

    /// <summary>A fraction as a CSS percentage (loop-motion endpoints: -0.35 → "-35%").</summary>
    public static string Percent(float fraction) =>
        $"{(fraction * 100).ToString("0.##", CultureInfo.InvariantCulture)}%";

    /// <summary>
    /// A style's font size: px, or <c>clamp(min, Nvw, max)</c> when it follows the window. The TS
    /// <c>fontSize()</c> twin writes the same string.
    /// </summary>
    public static string FontSize(TypeStyle style) => style.Fluid is { } fluid
        ? $"clamp({Px(fluid.Min)}, {Number(fluid.PercentOfWindow)}vw, {Px(fluid.Max)})"
        : Px(style.Size);

    /// <summary>
    /// A style's line height: px, or the style's own ratio unitless when the size follows the window,
    /// since a line box in px stays put while the glyphs grow. The TS <c>lineHeight()</c> twin.
    /// </summary>
    public static string LineHeight(TypeStyle style) => style.Fluid is not null && style.Size > 0
        ? Ratio(style.LineHeight, style.Size)
        : Px(style.LineHeight);

    /// <summary>
    /// A style's letter spacing: px, or <c>em</c> at the style's own ratio when the size follows the
    /// window, so the tracking tightens with the glyphs. The TS <c>letterSpacing()</c> twin.
    /// </summary>
    public static string LetterSpacing(TypeStyle style) => style.Fluid is not null && style.Size > 0
        ? $"{Ratio(style.Tracking, style.Size)}em"
        : Px(style.Tracking);

    /// <summary>A ratio divided in DOUBLE, as the TS twin divides, so both print the same digits.</summary>
    private static string Ratio(float part, float whole) =>
        ((double)part / whole).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// One edge of a positioned box: a point offset, a fraction of the containing box, or both
    /// added (<c>calc(30% - 16px)</c>); null when neither is set. The TS <c>edge()</c> twin writes
    /// the same string.
    /// </summary>
    public static string? Edge(float? point, float? fraction) => (point, fraction) switch
    {
        (null, null) => null,
        ({ } p, null) => Px(p),
        (null, { } f) => Percent(f),
        ({ } p, { } f) when p == 0 => Percent(f),
        ({ } p, { } f) => $"calc({Percent(f)} {(p < 0 ? "-" : "+")} {Px(Math.Abs(p))})",
    };

    /// <summary>A shift by fractions of the box's OWN size, or null for none (<c>translate(-50%, -100%)</c>).</summary>
    public static string? Shift(float x, float y) =>
        x == 0 && y == 0 ? null : $"translate({(x == 0 ? "0" : Percent(x))}, {(y == 0 ? "0" : Percent(y))})";

    /// <summary>A bare invariant number ("0.####") — opacity, aspect-ratio, scale factors.</summary>
    public static string Number(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>The CSS keyword for a <see cref="PointerCursor"/> — the enum's names ARE the CSS
    /// ones, so this is only the kebab-case spelling of the multi-word members.</summary>
    public static string Cursor(PointerCursor cursor) => cursor switch
    {
        PointerCursor.Pointer => "pointer",
        PointerCursor.Text => "text",
        PointerCursor.NotAllowed => "not-allowed",
        PointerCursor.Crosshair => "crosshair",
        PointerCursor.ColResize => "col-resize",
        PointerCursor.RowResize => "row-resize",
        _ => "default",
    };

    /// <summary>
    /// The S1 transform components as the equivalent CSS list — translate → rotate → scale, only the
    /// non-neutral parts, in that fixed order (the same order the native realizer's center-anchored
    /// matrix applies). Identity → null (no declaration).
    /// </summary>
    public static string? Transform(Transform2D transform)
    {
        if (transform.IsIdentity) return null;
        var parts = new List<string>(3);
        if (transform.TranslateX != 0 || transform.TranslateY != 0)
            parts.Add($"translate({Px(transform.TranslateX)}, {Px(transform.TranslateY)})");
        if (transform.RotationDegrees != 0)
            parts.Add($"rotate({Number(transform.RotationDegrees)}deg)");
        if (transform.ScaleX != 1 || transform.ScaleY != 1)
            parts.Add(transform.ScaleX == transform.ScaleY
                ? $"scale({Number(transform.ScaleX)})"
                : $"scale({Number(transform.ScaleX)}, {Number(transform.ScaleY)})");
        return string.Join(" ", parts);
    }

    /// <summary>A <see cref="Curve"/> as the CSS <c>cubic-bezier()</c> function.</summary>
    public static string Bezier(Curve curve) =>
        $"cubic-bezier({Number(curve.X1)}, {Number(curve.Y1)}, {Number(curve.X2)}, {Number(curve.Y2)})";

    /// <summary>
    /// Spec S6: a <see cref="TransitionSpec"/> as the CSS transition list — one entry per covered
    /// property, all riding the same duration/easing/delay. The channel → property mapping below is
    /// the NORMATIVE contract (the TS lowering mirrors it byte-for-byte; class identity across
    /// SSR/hydration depends on it). background-image rides Colors: gradient swaps snap where the
    /// browser can't interpolate them — same as the reference design.
    /// </summary>
    public static string Transition(TransitionSpec spec)
    {
        var timing = $"{Number(spec.DurationMs)}ms {Bezier(spec.Easing)}"
            + (spec.DelayMs > 0 ? $" {Number(spec.DelayMs)}ms" : "");
        return string.Join(", ", TransitionProperties(spec.Channels).Select(p => $"{p} {timing}"));
    }

    private static IEnumerable<string> TransitionProperties(StyleChannels channels)
    {
        if (channels.HasFlag(StyleChannels.Colors))
        {
            yield return "background-color";
            yield return "background-image";
            yield return "border-color";
            yield return "color";
            yield return "fill";
            yield return "stroke";
        }
        if (channels.HasFlag(StyleChannels.Opacity)) yield return "opacity";
        if (channels.HasFlag(StyleChannels.Transform)) yield return "transform";
        if (channels.HasFlag(StyleChannels.Shadow)) yield return "box-shadow";
        if (channels.HasFlag(StyleChannels.Filters))
        {
            yield return "filter";
            yield return "backdrop-filter";
        }
        if (channels.HasFlag(StyleChannels.Size))
        {
            yield return "width";
            yield return "height";
            yield return "max-width";
            yield return "max-height";
        }
    }

    /// <summary>The token gradient as a CSS background-image value — `light-dark()` stops keep the
    /// DOM mode-free exactly like solid fills. A <c>Via</c> midpoint lands as the third stop with
    /// its percentage position (the design's from/via/to triple).</summary>
    public static string Gradient(LinearGradient gradient)
    {
        var direction = gradient.Direction switch
        {
            GradientDirection.ToBottom => "to bottom",
            GradientDirection.ToBottomRight => "to bottom right",
            GradientDirection.ToBottomLeft => "to bottom left",
            _ => "to right",
        };
        var via = gradient.Via is { } mid
            ? $" {Value(mid)} {Number(gradient.ViaPosition * 100)}%,"
            : "";
        return $"linear-gradient({direction}, {Value(gradient.From)},{via} {Value(gradient.To)})";
    }

    /// <summary>The elliptical spotlight as a CSS <c>radial-gradient()</c> — explicit ellipse extent
    /// and a percentage center, the exact shape <see cref="RadialGradient"/> models.</summary>
    public static string Glow(RadialGradient glow) =>
        $"radial-gradient({Px(glow.RadiusX)} {Px(glow.RadiusY)} at "
        + $"{Number(glow.CenterX * 100)}% {Number(glow.CenterY * 100)}%, "
        + $"{Value(glow.From)}, {Value(glow.To)})";

    /// <summary>The grid as its TWO background-image layers (vertical rules, then horizontal) —
    /// each a hard-stop linear gradient that repeats at <c>background-size</c>.</summary>
    public static string GridPattern(GridPattern pattern)
    {
        var color = Value(pattern.Color);
        var line = Px(pattern.LineWidth);
        return $"linear-gradient(to right, {color} {line}, transparent {line}), "
             + $"linear-gradient(to bottom, {color} {line}, transparent {line})";
    }

    /// <summary>The <c>background-size</c> entry pair matching <see cref="GridPattern"/>'s layers.</summary>
    public static string GridPatternSize(GridPattern pattern)
    {
        var cell = Px(pattern.Cell);
        return $"{cell} {cell}, {cell} {cell}";
    }
}
