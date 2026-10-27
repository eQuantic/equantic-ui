using System.Globalization;
using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// Generates the NORMATIVE embedded stylesheet from the design tokens (docs/SHARED-COMPONENTS-PLAN.md:
/// "every CSS artifact the embedded engine ships is GENERATED at build time from the Primitives tokens
/// — hand-maintained CSS copies of token values are forbidden"). One theme in, one stylesheet out:
/// <c>color-scheme</c> setup, <c>--eq-*</c> custom properties (colors as <c>light-dark()</c> pairs,
/// radius/space scales, the theme's faces), the document's rules (its surface, text, face,
/// selection, placeholder and scrollbars), <c>.eq-type-*</c> classes for the type roles, and
/// <c>.eq-elevation-*</c> shadows. Parity with the C# tokens is TESTED, not promised.
/// <para>
/// Every page carries it, the theme the app chose or Photon's, and it comes before the app's own
/// CSS in the head, so an app's rule wins every tie with it.
/// </para>
/// </summary>
public static class ThemeStylesheet
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
    /// <param name="theme">The theme whose tokens become the custom properties.</param>
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

        // The document's chrome (#1038): what the page draws around the components.
        AppendColor(css, "selection", theme.Selection);
        if (theme.OnSelection is { } onSelection) AppendColor(css, "on-selection", onSelection);
        AppendColor(css, "placeholder", theme.Placeholder);
        AppendColor(css, "scrollbar-thumb", theme.ScrollbarThumb);
        AppendColor(css, "scrollbar-track", theme.ScrollbarTrack);

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

        // The theme's CODE FACE, as the variable both sides already read. `MonoStack` is
        // `var(--eq-font-mono, …)`, so declaring it here reaches the role class, every node that
        // says mono, the client's own lowering and the canvas measurer at once — without the
        // lowering having to learn about a theme it cannot see. The app keeps the last word: this
        // sheet comes before the app's own CSS, so an app's own `:root` rule wins.
        if (FaceName.Usable(theme.MonoFamily?.Name) is { } code)
            css.AppendLine($"  --eq-font-mono: \"{code}\", {TokenCss.MonoFallback};");
        // And its TEXT FACE, by the same rule: `SansStack` is `var(--eq-font-family, …)`, and the
        // document below and every role with no face of its own are set in it.
        if (FaceName.Usable(theme.TextFamily?.Name) is { } text)
            css.AppendLine($"  --eq-font-family: \"{text}\", {TokenCss.SansFallback};");

        // Motion (§06).
        css.AppendLine($"  --eq-motion-fast: {Motion.FastMs}ms;");
        css.AppendLine($"  --eq-motion-base: {Motion.BaseMs}ms;");
        css.AppendLine($"  --eq-motion-slow: {Motion.SlowMs}ms;");
        css.AppendLine($"  --eq-curve-standard: cubic-bezier({Cubic(Curve.Standard)});");
        css.AppendLine($"  --eq-curve-decelerate: cubic-bezier({Cubic(Curve.Decelerate)});");
        css.AppendLine($"  --eq-curve-accelerate: cubic-bezier({Cubic(Curve.Accelerate)});");
        css.AppendLine("}");

        // The DOCUMENT (#1038): the page around the components, from the theme rather than from a
        // block each app wrote. Ordinary selectors on purpose: this sheet comes before the app's own
        // CSS in the head, so an app's rule wins every tie by coming later. The placeholder's
        // opacity is the UA's: Firefox draws it at 54% unless told, which is not the theme's colour.
        css.AppendLine("body { margin: 0; background: var(--eq-color-background); color: var(--eq-color-text-primary); "
            + $"font-family: {TokenCss.SansStack}; -webkit-font-smoothing: antialiased; -moz-osx-font-smoothing: grayscale; }}");
        css.AppendLine(theme.OnSelection is null
            ? "::selection { background: var(--eq-color-selection); }"
            : "::selection { background: var(--eq-color-selection); color: var(--eq-color-on-selection); }");
        css.AppendLine("::placeholder { color: var(--eq-color-placeholder); opacity: 1; }");
        // The placeholder a page shows until its module mounts, when the server did not render it
        // (the shell's and boot.ts's): centred in the frame, as the shell's base block styled it.
        css.AppendLine(".eq-loading { display: flex; justify-content: center; align-items: center; height: 100vh; font-size: 1.5rem; }");
        // The page's own scrollbars only when the theme has an opinion on them. `scrollbar-color` is
        // inherited by every element, and Chromium ignores `::-webkit-scrollbar` wherever it is set,
        // so writing the defaults would switch off an app's own scrollbar CSS for a colour the
        // platform's scrollbars already follow through `color-scheme`. The SDK's own drawn
        // scrollbars (a table's) take the slots either way.
        if (theme.ScrollbarThumb != theme.TextMuted.WithOpacity(0.55f) || theme.ScrollbarTrack != theme.SurfaceSubtle)
            css.AppendLine(":root { scrollbar-color: var(--eq-color-scrollbar-thumb) var(--eq-color-scrollbar-track); }");

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
            css.AppendLine($"  font-size: {TokenCss.FontSize(style)};");
            css.AppendLine($"  line-height: {TokenCss.LineHeight(style)};");
            css.AppendLine($"  font-weight: {(int)style.Weight};");
            css.AppendLine($"  letter-spacing: {TokenCss.LetterSpacing(style)};");
            // The face a theme cut this ROLE in, on the class rather than inline on every node. The
            // client's lowering cannot read the theme's type scale — its component context is
            // opaque there — so a role face emitted inline by SSR would vanish on the first
            // client re-render. In the sheet both sides get it by not emitting anything.
            //
            // A mono ROLE is the same question with no family to name: it still has to reach the
            // element, and the stack is the answer. Its white-space belongs here too — a role that
            // is CODE keeps its indentation wherever it is used, and a rule the client cannot see
            // is a rule SSR must not emit inline either.
            var roleFace = FaceName.Usable(style.Family?.Name);
            if (roleFace is { } face) css.AppendLine($"  font-family: {TokenCss.Face(face, style.Mono)};");
            else if (style.Mono) css.AppendLine($"  font-family: {TokenCss.MonoStack};");
            if (style.Mono) css.AppendLine("  white-space: pre-wrap;");
            // And the slant, for the same reason as the face and the stack beside it: a theme may
            // cut a ROLE italic, the client's lowering cannot read the type scale to know, and a
            // slant SSR applies inline is a slant hydration drops.
            if (style.Italic) css.AppendLine("  font-style: italic;");
            css.AppendLine("}");

            // A form control does not inherit the document's face: the UA gives `input` and
            // `textarea` a font of their own, so something must say otherwise. It used to be an
            // inline `font-family: inherit` on every entry, which also beat the role's own family
            // — a themed brand worked for Text and never for a field. Said HERE instead, and only
            // for the roles whose class carries no family of its own, so the two never compete.
            if (roleFace is null && !style.Mono)
                css.AppendLine($".eq-entry.eq-type-{role.ToString().ToLowerInvariant()} "
                    + "{ font-family: inherit; }");
        }

        // Elevation (§05) — one class per level.
        for (var level = 0; level <= 5; level++)
        {
            css.AppendLine($".eq-elevation-{level} {{ box-shadow: {TokenCss.Shadow(theme.Elevation(level))}; }}");
        }

        // Interaction mechanics (spec §01 pressed = token swap; feedback at Fast motion). The VALUES
        // arrive per element as custom properties set by the realizers; only the mechanics live here.
        css.AppendLine(".eq-pressable { -webkit-tap-highlight-color: transparent; }");
        // The fill's fade is a DEFAULT, at zero specificity: at (0,2,0) it replaced the surface's own
        // transition (0,1,0), so a press that scales and a hover that lifts a control snapped,
        // measured in Chromium as `background-color 0.1s ease-out` on a surface that declared
        // `transform 0.2s` (#508). A surface that declares a transition keeps it.
        css.AppendLine(":where(.eq-pressable > :first-child) { transition: background-color var(--eq-motion-fast) ease-out; }");
        // Only a control that HAS a pressed fill swaps to it (eq-press-fill, set beside the custom
        // property): a var() with no value and no fallback makes the declaration compute to the
        // property's initial value, so every pressable without one went TRANSPARENT while pressed,
        // measured in Chromium as rgba(0, 0, 0, 0) (#508).
        css.AppendLine(".eq-press-fill:active > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        // A SIMULATED press (the Simulated node) reuses the same declaration rather than a copy of
        // it: a documentation gallery showing a state that drifted from the real one is worse than
        // showing none, and one selector list cannot drift from itself.
        css.AppendLine(".eq-pressed.eq-press-fill > :first-child { background-color: var(--eq-pressed-bg) !important; }");
        // Focus (spec §01): the double ring — 2dp Surface gap + 2dp FocusRing — on keyboard focus only
        // (:focus-visible). It sits on the CHILD so it follows the control's border-radius.
        css.AppendLine(".eq-pressable { outline: none; }");
        // INERT yields to its wrapper (the native dispatch twin): a disabled control inside an
        // enabled pressable lets the click reach the pressable that composed it — a Menu whose
        // trigger is a disabled-looking Button still opens. Without this the browser suppresses
        // the click on the disabled control entirely and the wrapper never hears it.
        css.AppendLine(".eq-pressable [disabled], .eq-pressable [aria-disabled=\"true\"] { pointer-events: none; }");
        // The ring is a slot in the child's OWN shadow list (TokenCss.RingSlot leads every list the
        // realizers write), never a rule of its own: a box-shadow rule here outranked the box's, so
        // keyboard focus took a raised or glowing control's elevation and glow away, where the
        // handoff has the ring "coexist with any fill" (#508). The property does not inherit, so a
        // shadowed box INSIDE the control draws no ring of its own, and a child with no shadow list
        // takes the ring from a rule of zero specificity that any list of its own outranks. A
        // SIMULATED focus (eq-focused) draws the same ring, as Photon's does.
        css.AppendLine("@property --eq-ring { syntax: \"*\"; inherits: false; }");
        css.AppendLine(".eq-pressable:focus-visible > :first-child, .eq-focused > :first-child "
            + "{ --eq-ring: 0 0 0 2px var(--eq-color-surface), 0 0 0 4px var(--eq-color-focus); }");
        css.AppendLine(":where(.eq-pressable:focus-visible > :first-child, .eq-focused > :first-child) "
            + "{ box-shadow: var(--eq-ring); }");

        // HIT SLOP (spec §08). `Touch.MinTarget`'s own doc promised it — "visuals may be smaller, the
        // framework expands hit-slop symmetrically" — and on the web nothing kept the promise: every
        // small pressable shipped with its visual as its hit rect, and the components that cared
        // (Slider, PageIndicator, SearchField's clear) each sized a target by hand. Photon has always
        // done it in one place (EmitVisitor.ExpandHitRect); this is the same contract, once, here.
        //
        // A PSEUDO-ELEMENT rather than padding: the target has to grow without moving anything, and
        // padding is layout. It is centred on the control and at least the minimum on each side, so
        // the growth is symmetric and needs to know nothing about the control's own size.
        //
        // The finger's minimum is gated on a COARSE pointer, which is the browser answering the
        // question Photon answers with Density: a pointer lands where it is aimed, and expanding a
        // dense toolbar's buttons to it would grow each one into its neighbour. A finger gets the
        // minimum, always.
        //
        // A FINE pointer keeps a floor of its own, Touch.MinPointerTarget, the minimum WCAG 2.2 SC
        // 2.5.8 asks of a target: a 20px checkbox was a 20px target, while a dense toolbar's 26px
        // buttons are past it and grow nothing (#430). Photon's Compact density is the same rule.
        HitSlop(css, "coarse", Touch.MinTarget);
        HitSlop(css, "fine", Touch.MinPointerTarget);

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
        // The panel already refused to outgrow the window ACROSS — `max-width: min(92vw, 420px)`
        // has been there since the start. This is the same refusal DOWN, and it was missing: a
        // panel taller than the window ran past the bottom of it with its content unreachable,
        // and every menu, dropdown and picker in the library shares this rule.
        //
        // 100vh and not less, because CSS cannot know where the panel STARTS: it is positioned
        // against its anchor, and the distance from there to the top of the window is a runtime
        // fact. So the default is the safety net — never taller than the whole window, scrolls
        // instead of overflowing — and an author who knows their own offset says it exactly:
        // `MaxHeight = SizeValue.WindowMinus(88)`. The default protects everyone; the vocabulary
        // is for being precise.
        css.AppendLine(".eq-anchor-panel { position: absolute; z-index: 1050; width: max-content; "
            + "max-width: min(92vw, 420px); max-height: 100vh; overflow-y: auto; }");
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
        // LONGHANDS, never the `animation` shorthand: a shorthand sets EVERY longhand it omits, so
        // this rule would write `animation-delay: 0s` over all eight bars. Each bar carries its own
        // delay as an atomic class, and a class alone (0,1,0) loses to a class-plus-type selector
        // (0,1,1). The stagger is the rotation, so losing it leaves a ring that pulses in place.
        css.AppendLine($".eq-spinner rect {{ animation-name: eq-spinner-fade; animation-duration: {Spinner.RevolutionMs}ms; animation-timing-function: linear; animation-iteration-count: infinite; }}");
        css.AppendLine("@keyframes eq-appear { to { opacity: 1; } }");
        css.AppendLine($".eq-spinner {{ opacity: 0; animation: eq-appear 1ms linear {Spinner.AppearDelayMs}ms forwards; }}");
        css.AppendLine("@media (prefers-reduced-motion: reduce) { .eq-spinner rect { animation-delay: 0ms !important; } }");

        // Code editor marks. Only MECHANICS live here: where a caret is is the engine's answer, and
        // its ink rides the node (an inverse slab writes with an ink of its own). What is left is
        // what a window gets from its host — the blink, and the fact that the caret does not show
        // while the surface does not hold the keyboard. The keyboard is held by the surface's
        // input, INSIDE it, which is why these read :focus-within. A sheet's entry is the code engine's
        // document, and its caret is the same mark behind the same kind of door.
        css.AppendLine("@keyframes eq-caret-blink { 0%, 49% { opacity: 1; } 50%, 100% { opacity: 0; } }");
        css.AppendLine($".eq-code-surface:focus-within .eq-code-caret, .eq-sheet-surface:focus-within .eq-code-caret {{ animation: eq-caret-blink {CodeSurface.CaretBlinkMs * 2}ms step-end infinite; }}");
        css.AppendLine(".eq-code-surface:not(:focus-within) .eq-code-caret, .eq-sheet-surface:not(:focus-within) .eq-code-caret { opacity: 0; }");
        // A steady caret for anyone who asked the OS to stop things moving — it still says where
        // you are, which is the whole point of it.
        css.AppendLine("@media (prefers-reduced-motion: reduce) { .eq-code-surface:focus-within .eq-code-caret, .eq-sheet-surface:focus-within .eq-code-caret { animation: none; } }");
        // The input a code surface types through: a real textarea, at the caret — so an input
        // method's candidate window opens where the text goes, and a phone raises its keyboard —
        // that nobody sees and nothing points at. It holds no text: every keystroke that reaches
        // it is taken out as an edit of the document and the textarea is left empty again.
        css.AppendLine(".eq-code-input { position: absolute; width: 1px; padding: 0; margin: 0; border: 0; outline: none; resize: none; overflow: hidden; white-space: pre; opacity: 0; color: transparent; background: transparent; caret-color: transparent; pointer-events: none; z-index: -1; }");

        // Text entry mechanics (spec B9): the input is chrome-less — the container shows focus —
        // and its placeholder is the document's, in the theme's Placeholder (above). Values are
        // tokens; only mechanics live here.
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
        // A heading is a heading for the OUTLINE, never for the paint: the type role already
        // carries size, weight and line height, and the browser's own h1–h6 rules would add a
        // margin and a font size on top of it. Zeroed here rather than per element, so choosing a
        // level costs nothing in the atomic sheet.
        //
        // `display` is in the list for the reason the others are not enough. A Text has always
        // lowered to a SPAN, which is inline; a heading element is block. Inside a flex or grid
        // parent that difference is erased (a child is blockified either way), but in inline flow
        // it is a line break the author never asked for — the one place where marking up the
        // outline would still have moved the page. Every h1–h6 on a page comes from a Text, so
        // the rule is exactly as scoped as it looks.
        css.AppendLine("h1, h2, h3, h4, h5, h6 { display: inline; margin: 0; font-size: inherit; "
            + "font-weight: inherit; line-height: inherit; }");
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
        css.AppendLine(".eq-desc, .eq-announcement { position: absolute; width: 1px; height: 1px; margin: -1px; border: 0; padding: 0; clip-path: inset(50%); overflow: hidden; white-space: nowrap; }");

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

    /// <summary>
    /// The hit slop for one kind of pointer: a pressable's target, centred on it and at least
    /// <paramref name="minimum"/> on each side, without moving anything. The pressable is the slop's
    /// containing block, and the child the lift below positions is the containing block of what the
    /// content positions absolutely: the same box, since the pressable is a button with no padding
    /// around one child (a badge on a button anchors to the button either way), and fixed layers are
    /// unaffected by `relative`.
    /// <para>
    /// The target lies UNDER the control's own content, so it answers only where the control draws
    /// nothing: the slop around a small one. It is the `::before`, first in tree order, and the
    /// content is positioned like it, so the content paints and is hit after it, as Flutter's hit test
    /// asks a child before its parent. A target over the content took every hit the content
    /// should have had: under a fine pointer a Button's own box never matched `:hover`, so no button
    /// showed its hover fill, and a Pressable around an IconButton took the inner control's hits
    /// (#430, measured in a browser). The lift has no specificity, so a child that positions itself
    /// (a raised box, a layer of a Stack) keeps its own. A child that draws no box of its own
    /// (`display: contents`: an InView, an Adaptive, a light and dark Image) cannot be positioned,
    /// so the realizers mark the first descendants that draw one, through any chain of such
    /// wrappers, with `eq-lift`, and the lift reaches them (#622).
    /// </para>
    /// </summary>
    private static void HitSlop(StringBuilder css, string pointer, float minimum)
    {
        css.AppendLine($"@media (pointer: {pointer}) {{");
        css.AppendLine("  .eq-pressable { position: relative; }");
        css.AppendLine("  .eq-pressable::before { content: \"\"; position: absolute; top: 50%; left: 50%; "
            + $"width: 100%; height: 100%; min-width: {TokenCss.Px(minimum)}; min-height: {TokenCss.Px(minimum)}; "
            + "transform: translate(-50%, -50%); }");
        css.AppendLine("  :where(.eq-pressable) > *, :where(.eq-lift) { position: relative; }");
        css.AppendLine("}");
    }

    private static string Cubic(Curve curve) =>
        string.Create(CultureInfo.InvariantCulture, $"{curve.X1}, {curve.Y1}, {curve.X2}, {curve.Y2}");
}
