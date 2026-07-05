using System.Globalization;
using System.Text;
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

    /// <summary>An elevation level as a box-shadow value (offset/blur/spread + light-dark color).</summary>
    public static string Shadow(ShadowSpec spec) => spec.IsNone
        ? "none"
        : $"0 {Px(spec.OffsetY)} {Px(spec.Blur)} {Px(spec.Spread)} {Value(spec.Color)}";

    /// <summary>A fraction as a CSS percentage (loop-motion endpoints: -0.35 → "-35%").</summary>
    public static string Percent(float fraction) =>
        $"{(fraction * 100).ToString("0.##", CultureInfo.InvariantCulture)}%";

    /// <summary>The 2-stop token gradient as a CSS background-image value — `light-dark()` stops
    /// keep the DOM mode-free exactly like solid fills.</summary>
    public static string Gradient(LinearGradient gradient)
    {
        var direction = gradient.Direction == GradientDirection.ToBottom ? "to bottom" : "to right";
        return $"linear-gradient({direction}, {Value(gradient.From)}, {Value(gradient.To)})";
    }
}

/// <summary>
/// Generates the NORMATIVE embedded stylesheet from the design tokens (docs/SHARED-COMPONENTS-PLAN.md:
/// "every CSS artifact the embedded engine ships is GENERATED at build time from the Primitives tokens
/// — hand-maintained CSS copies of token values are forbidden"). One theme in, one stylesheet out:
/// <c>color-scheme</c> setup, <c>--eq-*</c> custom properties (colors as <c>light-dark()</c> pairs,
/// radius/space scales), <c>.eq-type-*</c> classes for the type roles, and <c>.eq-elevation-*</c>
/// shadows. Parity with the C# tokens is TESTED, not promised.
/// </summary>
public static class PhotonCssGenerator
{
    public static string Generate(IAppTheme theme)
    {
        var css = new StringBuilder();
        css.AppendLine("/* GENERATED from eQuantic.UI.Primitives design tokens — do not edit (docs/SHARED-COMPONENTS-PLAN.md). */");
        css.AppendLine(":root {");
        css.AppendLine("  color-scheme: light dark;");

        // Surfaces, text tiers, interaction chrome (§01).
        AppendColor(css, "background", theme.Background);
        AppendColor(css, "surface", theme.Surface);
        AppendColor(css, "surface-subtle", theme.SurfaceSubtle);
        AppendColor(css, "surface-highlight", theme.SurfaceHighlight);
        AppendColor(css, "border", theme.Border);
        AppendColor(css, "border-strong", theme.BorderStrong);
        AppendColor(css, "text-primary", theme.TextPrimary);
        AppendColor(css, "text-secondary", theme.TextSecondary);
        AppendColor(css, "text-muted", theme.TextMuted);
        AppendColor(css, "text-inverse", theme.TextInverse);
        AppendColor(css, "focus", theme.FocusRing);
        AppendColor(css, "link", theme.LinkColor);
        AppendColor(css, "scrim", theme.Scrim);

        // Interactive variants — the five sub-tokens each (§01).
        foreach (var variant in new[]
                 {
                     Variant.Primary, Variant.Secondary, Variant.Destructive,
                     Variant.Success, Variant.Warning, Variant.Info,
                 })
        {
            var name = variant.ToString().ToLowerInvariant();
            var colors = theme.Colors(variant);
            AppendColor(css, $"{name}-base", colors.Base);
            AppendColor(css, $"{name}-on", colors.OnBase);
            AppendColor(css, $"{name}-pressed", colors.Pressed);
            AppendColor(css, $"{name}-subtle", colors.Subtle);
            AppendColor(css, $"{name}-on-subtle", colors.OnSubtle);
        }

        // Radius scale (§04) and spacing ladder (§03).
        css.AppendLine($"  --eq-radius-xs: {TokenCss.Px(Radius.Xs)};");
        css.AppendLine($"  --eq-radius-sm: {TokenCss.Px(Radius.Sm)};");
        css.AppendLine($"  --eq-radius-md: {TokenCss.Px(Radius.Md)};");
        css.AppendLine($"  --eq-radius-lg: {TokenCss.Px(Radius.Lg)};");
        css.AppendLine($"  --eq-radius-xl: {TokenCss.Px(Radius.Xl)};");
        css.AppendLine("  --eq-radius-full: 9999px;");
        foreach (var (name, value) in new (string, float)[]
                 {
                     ("s1", Space.S1), ("s2", Space.S2), ("s3", Space.S3), ("s4", Space.S4), ("s5", Space.S5),
                     ("s6", Space.S6), ("s8", Space.S8), ("s10", Space.S10), ("s12", Space.S12), ("s16", Space.S16),
                 })
        {
            css.AppendLine($"  --eq-space-{name}: {TokenCss.Px(value)};");
        }

        // Motion (§06).
        css.AppendLine($"  --eq-motion-fast: {Motion.FastMs}ms;");
        css.AppendLine($"  --eq-motion-base: {Motion.BaseMs}ms;");
        css.AppendLine($"  --eq-motion-slow: {Motion.SlowMs}ms;");
        css.AppendLine($"  --eq-curve-standard: cubic-bezier({Cubic(Curve.Standard)});");
        css.AppendLine($"  --eq-curve-decelerate: cubic-bezier({Cubic(Curve.Decelerate)});");
        css.AppendLine($"  --eq-curve-accelerate: cubic-bezier({Cubic(Curve.Accelerate)});");
        css.AppendLine("}");

        // Type roles (§02) — one class per role; components reference the class, never raw sizes.
        foreach (var role in new[]
                 {
                     TypeRole.Display, TypeRole.Heading, TypeRole.Title, TypeRole.BodyL,
                     TypeRole.BodyM, TypeRole.Label, TypeRole.Caption,
                 })
        {
            var style = theme.Type(role);
            css.AppendLine($".eq-type-{role.ToString().ToLowerInvariant()} {{");
            css.AppendLine($"  font-size: {TokenCss.Px(style.Size)};");
            css.AppendLine($"  line-height: {TokenCss.Px(style.LineHeight)};");
            css.AppendLine($"  font-weight: {(int)style.Weight};");
            css.AppendLine($"  letter-spacing: {TokenCss.Px(style.Tracking)};");
            css.AppendLine("}");
        }

        // Elevation (§05) — one class per level.
        for (var level = 0; level <= 5; level++)
        {
            css.AppendLine($".eq-elevation-{level} {{ box-shadow: {TokenCss.Shadow(theme.Elevation(level))}; }}");
        }

        // Interaction mechanics (spec §01 pressed = token swap; feedback at Fast motion). The VALUES
        // arrive per element as custom properties set by the realizers; only the mechanics live here.
        css.AppendLine(".eq-pressable { -webkit-tap-highlight-color: transparent; }");
        css.AppendLine(".eq-pressable > :first-child { transition: background-color var(--eq-motion-fast) ease-out; }");
        css.AppendLine(".eq-pressable:active > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        // Focus (spec §01): the double ring — 2dp Surface gap + 2dp FocusRing — on keyboard focus only
        // (:focus-visible). The shadow sits on the CHILD so it follows the control's border-radius.
        css.AppendLine(".eq-pressable { outline: none; }");
        css.AppendLine(".eq-pressable:focus-visible > :first-child { box-shadow: 0 0 0 2px var(--eq-color-surface), 0 0 0 4px var(--eq-color-focus); }");

        // Overlay layer (Phase C): the viewport-fixed stacking layer — composition (scrim,
        // centering) belongs to the component; only the layer mechanics live here.
        css.AppendLine(".eq-overlay { position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: 1000; }");

        // Text entry mechanics (spec B9): the input is chrome-less — the container shows focus —
        // and the placeholder rides TextMuted. Values are tokens; only mechanics live here.
        css.AppendLine(".eq-entry { outline: none; }");
        css.AppendLine(".eq-entry::placeholder { color: var(--eq-color-text-muted); }");

        // Loop motion (spec §06, transform-only): ONE keyframe pair per effect reads its per-element
        // endpoints from custom properties the realizers set at the style tail; duration rides the
        // animation shorthand. `prefers-reduced-motion` statically replaces movement — the browser
        // twin of PhotonHost.ReducedMotion (native renders at rest and stops requesting frames).
        css.AppendLine("@keyframes eq-slide-x { 0% { transform: translateX(var(--eq-loop-from)); } 100% { transform: translateX(var(--eq-loop-to)); } }");
        css.AppendLine("@media (prefers-reduced-motion: reduce) { .eq-loop { animation: none; } .eq-loop-rest-hidden { visibility: hidden; } }");

        return css.ToString();
    }

    private static void AppendColor(StringBuilder css, string name, ColorToken token) =>
        css.AppendLine($"  --eq-color-{name}: {TokenCss.Value(token)};");

    private static string Cubic(Curve curve) =>
        string.Create(CultureInfo.InvariantCulture, $"{curve.X1}, {curve.Y1}, {curve.X2}, {curve.Y2}");
}
