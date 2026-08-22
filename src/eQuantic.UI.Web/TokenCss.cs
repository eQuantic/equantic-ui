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
    /// <summary>
    /// The monospace stack (Text.Mono / rich runs) with an APP-SETTABLE hook in front of it —
    /// ui-monospace first inside the fallback, so each OS uses its native mono face.
    /// <para>
    /// A mono run lands on an atomic class, and an atomic class beats a <c>body</c> rule, so an app
    /// that wanted its own code face had nowhere to put it: the class name is a content hash that
    /// moves between builds, which is no hook at all. Setting <c>--eq-font-mono</c> on <c>:root</c>
    /// is the hook.
    /// </para>
    /// <para>
    /// This string must stay CHARACTER-IDENTICAL to the runtime's MONO_STACK (lowering.ts): the
    /// atomic class name is a hash of the declaration, so a server that emits one spelling and a
    /// client that emits another mint two classes for one style and the page re-paints on
    /// hydration.
    /// </para>
    /// </summary>
    public const string MonoStack =
        "var(--eq-font-mono, ui-monospace, 'SFMono-Regular', Menlo, Consolas, monospace)";

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
    /// <param name="mode">
    /// The mode the app DECLARED, or <c>null</c> to follow the operating system.
    /// <para>
    /// This drives <c>color-scheme</c>, and it has to: every token resolves through
    /// <c>light-dark()</c>, which reads <c>color-scheme</c> and nothing else. Declaring a mode
    /// anywhere else — on a controller, in an option — while this said <c>light dark</c> left two
    /// answers to one question: the page painted whatever the OS wanted while the toggle reported
    /// what the app had asked for, so with a dark desktop the site came up dark and offered to
    /// "switch to dark".
    /// </para>
    /// <para>
    /// Null stays the default and stays honest: the page follows the OS, which is what most apps
    /// want, and the toggle's label is only settled once the browser's own controller resolves it
    /// at hydration — a server cannot know a preference it was never sent.
    /// </para>
    /// </param>
    public static string Generate(IAppTheme theme, ThemeMode? mode = null)
    {
        var css = new StringBuilder();
        css.AppendLine("/* GENERATED from eQuantic.UI.Primitives design tokens — do not edit (docs/SHARED-COMPONENTS-PLAN.md). */");
        css.AppendLine(":root {");
        css.AppendLine($"  color-scheme: {mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            _ => "light dark",
        }};");

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
                     Variant.Success, Variant.Warning, Variant.Info, Variant.Tertiary,
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

        // Shape scale (§04) — theme-sourced (Material overrides the ladder) — and spacing (§03).
        css.AppendLine($"  --eq-radius-xs: {TokenCss.Px(theme.Shape(ShapeScale.ExtraSmall))};");
        css.AppendLine($"  --eq-radius-sm: {TokenCss.Px(theme.Shape(ShapeScale.Small))};");
        css.AppendLine($"  --eq-radius-md: {TokenCss.Px(theme.Shape(ShapeScale.Medium))};");
        css.AppendLine($"  --eq-radius-lg: {TokenCss.Px(theme.Shape(ShapeScale.Large))};");
        css.AppendLine($"  --eq-radius-xl: {TokenCss.Px(theme.Shape(ShapeScale.ExtraLarge))};");
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
        //
        // Over the ENUM, never a hand-written list. It was a list, and it stopped at Caption while
        // the scale grew its dense end (TitleSmall, LabelSmall, Overline) — so the realizer stamped
        // `.eq-type-labelsmall` on elements that had no such rule behind them and they silently
        // inherited the body's 16px/400, a fifth too large, in exactly the chrome those rungs exist
        // for. Nothing failed; the type was just wrong. Enumerating makes the drift impossible.
        foreach (var role in Enum.GetValues<TypeRole>())
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
        // A SIMULATED press (the Simulated node) reuses the same declaration rather than a copy of
        // it: a documentation gallery showing a state that drifted from the real one is worse than
        // showing none, and one selector list cannot drift from itself.
        css.AppendLine(".eq-pressed > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        // Focus (spec §01): the double ring — 2dp Surface gap + 2dp FocusRing — on keyboard focus only
        // (:focus-visible). The shadow sits on the CHILD so it follows the control's border-radius.
        css.AppendLine(".eq-pressable { outline: none; }");
        // INERT yields to its wrapper (the native dispatch twin): a disabled control inside an
        // enabled pressable lets the click reach the pressable that composed it — a Menu whose
        // trigger is a disabled-looking Button still opens. Without this the browser suppresses
        // the click on the disabled control entirely and the wrapper never hears it.
        css.AppendLine(".eq-pressable [disabled], .eq-pressable [aria-disabled=\"true\"] { pointer-events: none; }");
        css.AppendLine(".eq-pressable:focus-visible > :first-child { box-shadow: 0 0 0 2px var(--eq-color-surface), 0 0 0 4px var(--eq-color-focus); }");

        // HIT SLOP (spec §08). `Touch.MinTarget`'s own doc promised it — "visuals may be smaller, the
        // framework expands hit-slop symmetrically" — and on the web nothing kept the promise: every
        // small pressable shipped with its visual as its hit rect, and the components that cared
        // (Slider, PageIndicator, SearchField's clear) each sized a target by hand. Photon has always
        // done it in one place (PhotonRealizer.ExpandHitRect); this is the same contract, once, here.
        //
        // A PSEUDO-ELEMENT rather than padding: the target has to grow without moving anything, and
        // padding is layout. It is centred on the control and at least the minimum on each side, so
        // the growth is symmetric and needs to know nothing about the control's own size.
        //
        // Gated on a COARSE pointer, which is the browser answering the question Photon answers with
        // Density: a pointer lands where it is aimed, and expanding a dense toolbar's buttons would
        // grow each one into its neighbour. A finger gets the minimum, always.
        css.AppendLine("@media (pointer: coarse) {");
        // The containing block for the target below. It is the framework's own wrapper element, and
        // an absolutely positioned descendant of a pressable anchoring to the pressable is the more
        // correct answer anyway (a badge on a button); fixed layers are unaffected by `relative`.
        css.AppendLine("  .eq-pressable { position: relative; }");
        css.AppendLine("  .eq-pressable::after { content: \"\"; position: absolute; top: 50%; left: 50%; "
            + $"width: 100%; height: 100%; min-width: {TokenCss.Px(Touch.MinTarget)}; min-height: {TokenCss.Px(Touch.MinTarget)}; "
            + "transform: translate(-50%, -50%); }");
        css.AppendLine("}");

        // Overlay layer (Phase C): the viewport-fixed stacking layer — composition (scrim,
        // centering) belongs to the component; only the layer mechanics live here. The single
        // grid cell hands the child the VIEWPORT'S size: a layer lays out against the viewport
        // by contract, and without this a child that hugs (Drawer's bare Stack) collapses to its
        // content while its Fill-sized descendants resolve against zero. Anchored and Positioned
        // children are absolute, so the stretch changes nothing they do.
        css.AppendLine(".eq-overlay { position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: 1000; display: grid; }");
        // Non-modal layers (toasts): input passes through except on the layer's own pressables.
        css.AppendLine(".eq-overlay-passthrough { pointer-events: none; }");
        css.AppendLine(".eq-overlay-passthrough .eq-pressable { pointer-events: auto; }");
        // Wave 3 anchored overlays: relative host, absolute panel (pure CSS positioning — the
        // placement classes are the whole mechanism), invisible fixed scrim for tap-outside.
        css.AppendLine(".eq-anchorhost { position: relative; display: flex; flex-direction: column; }");
        css.AppendLine(".eq-anchor-scrim { position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: 1040; background: transparent; border: none; padding: 0; margin: 0; appearance: none; }");
        // `max-content`, and it is not a nicety: an absolutely positioned panel sizes shrink-to-fit
        // against its CONTAINING BLOCK, which here is the anchor — a 24dp icon button. Every menu
        // therefore collapsed onto its own min-width and wrapped any label longer than that ("
        // Workspace settings" came out on two lines inside a one-line row). The panel measures its
        // CONTENT instead, still bounded: never wider than the viewport it must stay inside.
        css.AppendLine(".eq-anchor-panel { position: absolute; z-index: 1050; width: max-content; max-width: min(92vw, 420px); }");
        // A PAINTED scrim (ScrimStyle) must dim the page, never its own anchor: the anchor lifts
        // between the scrim (1040) and the panel (1050).
        css.AppendLine(".eq-anchor-above { position: relative; z-index: 1045; }");
        css.AppendLine(".eq-anchor-b-start { top: 100%; inset-inline-start: 0; }");
        css.AppendLine(".eq-anchor-b-end { top: 100%; inset-inline-end: 0; }");
        css.AppendLine(".eq-anchor-t-start { bottom: 100%; inset-inline-start: 0; }");
        css.AppendLine(".eq-anchor-t-end { bottom: 100%; inset-inline-end: 0; }");
        css.AppendLine(".eq-anchor-match { min-width: 100%; }");
        css.AppendLine(".eq-anchor-b-center { top: 100%; inset-inline-start: 50%; transform: translateX(-50%); }");
        css.AppendLine(".eq-anchor-t-center { bottom: 100%; inset-inline-start: 50%; transform: translateX(-50%); }");
        // Wave 3b hover reveal (the Tooltip mechanism): the panel exists in the DOM but only shows
        // while the HOST is hovered — pure CSS, zero JS, never on touch (hover media handled by
        // the browser's hover emulation rules).
        css.AppendLine(".eq-hoverreveal > .eq-anchor-panel { opacity: 0; pointer-events: none; transition: opacity 120ms ease-out; }");
        css.AppendLine(".eq-hoverreveal:hover > .eq-anchor-panel { opacity: 1; }");
        // Keyboard FOCUS reveals the same panel — :focus-visible, not :focus-within, because a
        // tooltip popping on every mouse click sits in the way; keyboard users are the ones who
        // cannot hover. Still zero JS.
        css.AppendLine(".eq-hoverreveal:has(:focus-visible) > .eq-anchor-panel { opacity: 1; }");
        // WCAG 1.4.13: Escape hides a revealed panel WITHOUT moving pointer or focus. The class is
        // set by the runtime's suppression controller and cleared when the trigger condition ends
        // (mouseleave/focusout) — !important because it must beat both reveal rules above.
        css.AppendLine(".eq-hoverreveal.eq-reveal-suppressed > .eq-anchor-panel { opacity: 0 !important; }");

        // Link mechanics: the anchor is pure semantics — the child owns every visual (the Pressable
        // contract). display:block keeps flex/stack sizing natural.
        css.AppendLine(".eq-link { display: block; text-decoration: none; color: inherit; }");

        // A themed IMAGE pair (Image.DarkSource): both artworks ship, CSS shows one.
        //
        // It has to follow the SAME decision the palette follows, and it did not: the colours
        // resolve through `light-dark()` (which reads `color-scheme`) while this pair read
        // `prefers-color-scheme` — the OS, and only the OS. So an app that declared a mode got a
        // dark palette carrying the light logo, and no amount of app-side code could fix it: the
        // theme switch deliberately does NOT re-render (it flips one declaration), so choosing the
        // artwork in C# would freeze it at the first paint instead.
        //
        // A declared mode therefore decides outright, with no media query to disagree with it. The
        // data-theme rules below stay in every case: the browser's controller stamps it on a
        // toggle, and a live choice outranks a build-time default.
        css.AppendLine(".eq-themed-dark { display: none; }");
        css.AppendLine(mode switch
        {
            ThemeMode.Light => ".eq-themed-light { display: block; }",
            ThemeMode.Dark => ".eq-themed-light { display: none; } .eq-themed-dark { display: block; }",
            _ => "@media (prefers-color-scheme: dark) { .eq-themed-light { display: none; } .eq-themed-dark { display: block; } }",
        });
        css.AppendLine(":root[data-theme=\"dark\"] .eq-themed-light { display: none; }");
        css.AppendLine(":root[data-theme=\"dark\"] .eq-themed-dark { display: block; }");
        css.AppendLine(":root[data-theme=\"light\"] .eq-themed-light { display: block; }");
        css.AppendLine(":root[data-theme=\"light\"] .eq-themed-dark { display: none; }");

        // Enter motion (spec §06 — Presence): a mount-playing animation on the subtree's own root
        // element (no wrapper — the class rides the lowered child, so flex/stack layout is
        // untouched). Reduce Motion replaces movement with the spec's short crossfade.
        css.AppendLine("@keyframes eq-presence-fade { from { opacity: 0; } }");
        css.AppendLine($"@keyframes eq-presence-slideup {{ from {{ opacity: 0; transform: translateY({TokenCss.Px(Presence.SlideDistance)}); }} }}");
        css.AppendLine(".eq-presence-fade { animation: eq-presence-fade var(--eq-motion-base) ease-out; }");
        css.AppendLine(".eq-presence-slideup { animation: eq-presence-slideup var(--eq-motion-base) ease-out; }");
        // Exit motion: the reconciler adds the exit class on removal (the layer reparents to body
        // while it plays) — Fast duration, held at the end state until the deferred removal lands.
        // These rules sit AFTER the enter rules, so swapping the class retargets the animation.
        css.AppendLine("@keyframes eq-presence-exit-fade { to { opacity: 0; } }");
        css.AppendLine($"@keyframes eq-presence-exit-slideup {{ to {{ opacity: 0; transform: translateY({TokenCss.Px(Presence.SlideDistance)}); }} }}");
        css.AppendLine(".eq-presence-exit-fade { animation: eq-presence-exit-fade var(--eq-motion-fast) ease-in forwards; }");
        css.AppendLine(".eq-presence-exit-slideup { animation: eq-presence-exit-slideup var(--eq-motion-fast) ease-in forwards; }");
        css.AppendLine($"@media (prefers-reduced-motion: reduce) {{ .eq-presence-fade, .eq-presence-slideup {{ animation: eq-presence-fade {Motion.ReducedCrossfadeMs}ms ease-out; }} .eq-presence-exit-fade, .eq-presence-exit-slideup {{ animation: eq-presence-exit-fade {Motion.ReducedCrossfadeMs}ms ease-in forwards; }} }}");

        // Spinner mechanics (spec B15): the 800ms sawtooth fade each bar rides with its own
        // negative delay (the rotation phase); the 400ms anti-flash appear; Reduce Motion zeroes
        // the stagger so the bars pulse IN PLACE — same fade, no rotation phase.
        css.AppendLine($"@keyframes eq-spinner-fade {{ from {{ opacity: 1; }} to {{ opacity: 0.3; }} }}");
        css.AppendLine($".eq-spinner rect {{ animation: eq-spinner-fade {Spinner.RevolutionMs}ms linear infinite; }}");
        css.AppendLine("@keyframes eq-appear { to { opacity: 1; } }");
        css.AppendLine($".eq-spinner {{ opacity: 0; animation: eq-appear 1ms linear {Spinner.AppearDelayMs}ms forwards; }}");
        css.AppendLine("@media (prefers-reduced-motion: reduce) { .eq-spinner rect { animation-delay: 0ms !important; } }");

        // Code editor marks. Only MECHANICS live here: where the caret and the band are is
        // arithmetic the realizers do, and their ink rides the node (an inverse slab writes with an
        // ink of its own). What is left is what a window gets from its host — the blink, and the
        // fact that neither mark shows while the surface does not hold the caret.
        css.AppendLine("@keyframes eq-caret-blink { 0%, 49% { opacity: 1; } 50%, 100% { opacity: 0; } }");
        css.AppendLine($".eq-code-surface:focus .eq-code-caret {{ animation: eq-caret-blink {CodeSurface.CaretBlinkMs * 2}ms step-end infinite; }}");
        css.AppendLine(".eq-code-surface:not(:focus) .eq-code-caret { opacity: 0; }");
        // A steady caret for anyone who asked the OS to stop things moving — it still says where
        // you are, which is the whole point of it.
        css.AppendLine("@media (prefers-reduced-motion: reduce) { .eq-code-surface:focus .eq-code-caret { animation: none; } }");

        // Text entry mechanics (spec B9): the input is chrome-less — the container shows focus —
        // and the placeholder rides TextMuted. Values are tokens; only mechanics live here.
        css.AppendLine(".eq-entry { outline: none; }");
        // The .eq-field shell FILLS its container and centres what is in it. Without this it was a
        // plain block of content height, so an entry dropped straight into a fixed-height Box —
        // which is what a hand-built field does, and what the framework's own TextInput avoids
        // only because it wraps the entry in a centred Row — sat against the top edge: measured at
        // 0px above and 24px below inside a 44dp box, with the caret and the placeholder up there
        // with it. Centring here fixes every composition instead of asking each one to remember a
        // Row, and a multi-line entry is unaffected (its height is its own, and centring a single
        // child that already fills the line is a no-op).
        css.AppendLine(".eq-field { display: flex; align-items: center; height: 100%; }");
        css.AppendLine(".eq-entry::placeholder { color: var(--eq-color-text-muted); }");
        // AUTOFILL. Chrome paints its own background and text colour over an autofilled control,
        // ignoring ours — so a themed field showed a pale rectangle floating inside it, in the one
        // flow (a filled form) where the app looks least finished. Clipping that background to the
        // TEXT makes it invisible while leaving the field's own surface alone, and the fill colour
        // and caret come back to the theme. The state sticks through hover, focus and active, so
        // every one of them has to say it.
        css.AppendLine(".eq-entry:-webkit-autofill, .eq-entry:-webkit-autofill:hover, "
            + ".eq-entry:-webkit-autofill:focus, .eq-entry:-webkit-autofill:active { "
            + "-webkit-background-clip: text; background-clip: text; "
            + "-webkit-text-fill-color: var(--eq-color-text-primary); "
            + "caret-color: var(--eq-color-text-primary); }");
        // The entry's description twin: clipped, NOT display:none — a hidden target still reads
        // for aria-describedby, but only a rendered one announces as a live region.
        css.AppendLine(".eq-desc { position: absolute; width: 1px; height: 1px; margin: -1px; border: 0; padding: 0; clip-path: inset(50%); overflow: hidden; white-space: nowrap; }");

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
