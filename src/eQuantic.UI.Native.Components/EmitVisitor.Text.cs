using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Everything whose pixels are glyphs: the three nodes that carry text, and the raster,
/// caret, selection and placeholder machinery they share.
/// </summary>
internal sealed partial class EmitVisitor
{
    private void EmitTextNode(Text text, EmitState s)
    {
        EmitText(s.Node, text, s.Theme, s.Mode, s.Builder, s.Motion);
        // A LINKED RUN is a navigation surface of its own: the layout knows which pixels the words
        // cover, and nothing else does on a target that draws its own glyphs. The POINTER registers
        // one region per piece, so a link that wraps is pressable on both its lines; the KEYBOARD
        // gets one stop for the link, under the identity RichTextRuns gives it (#255).
        foreach (var link in RichTextRuns.LinksOf(s.Node, text))
        {
            foreach (var rect in link.Rects)
                s.Input.Add(new LinkRegion(rect, link.Destination, link.Path));
            s.Input.Add(new FocusStop(link.Path, null, null, link.Bounds));

            // Focused by PATH alone: a run is not a node, so nothing up the dispatch could have
            // recognised it and set the pending ring.
            if (s.Press.FocusedPath == link.Path) FocusRing(s, link.Bounds, default);
        }
    }

    // Spec B9 fence: the entry renders the W4 one-line placeholder bar — value in
    // TextPrimary, empty shows the placeholder in TextMuted. Caret/selection/IME land at M4.
    private void EmitTextEntry(TextEntry entry, EmitState s)
    {
        EmitEntry(s.Node, entry, s.Theme, s.Mode, s.Builder, s.Input, s.Press, s.Motion);
    }

    private void EmitCode(CodeSurface surface, EmitState s)
    {
        EmitCodeSurface(s.Node, surface, s.Theme, s.Mode, s.Builder, s.Input, s.Press, s.Motion);
    }

    /// <summary>2dp: thin enough to sit between glyphs, thick enough to see on a scaled display.</summary>
    private const float CaretWidth = 2f;

    /// <summary>How much of the band shows through — normative for BOTH targets (the web mirror
    /// paints the same number).</summary>
    internal const float SelectionAlpha = 0.28f;

    /// <summary>How close to the right edge the caret may ride before the text slides: enough to
    /// see the caret itself plus a sliver of what comes next.</summary>
    private const float CaretFollowMargin = 8f;

    /// <summary>
    /// W4: REAL text when the platform service is present — one A8 raster per block (cached by
    /// content/style/width/scale/alignment; the tint carries the color, so one raster serves both
    /// modes), drawn as a single Texture command over the node bounds.
    /// <para>
    /// With NO service, the deterministic placeholder bars (tests, headless): one soft bar per
    /// measured line, 55% of the line box in the text color at 30% alpha, until the
    /// HarfBuzz/FreeType stack lands. Unmistakably a placeholder, and it verifies layout geometry
    /// in goldens — regenerating those when real glyphs arrive is by design.
    /// </para>
    /// <para>
    /// Two summaries used to sit here, one per half, which is not valid on one member: the second
    /// silently won and the placeholder half went undocumented wherever docs are read from the
    /// assembly.
    /// </para>
    /// </summary>
    private static void EmitText(LayoutNode node, Text text, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder, MotionScope motion)
    {
        // A RICH paragraph is drawn PIECE BY PIECE — one raster per run per line, each in its own
        // style. Drawn as one raster of the joined text, a sentence lost its bold, its inline code
        // and its links at once, and the native side of every markdown page was flat prose.
        if (motion.TextRasterizer is { } runRasterizer && node.TextRuns is { Count: > 0 } fragments)
        {
            var cache = motion.TextCache ?? TextRasterCache.Shared;
            var baseColor = (text.Color ?? theme.TextPrimary).Resolve(mode);
            // Alignment cannot ride the raster here — each fragment is its own one-word raster and
            // the LINE is what gets aligned, so the offset goes on the piece's x. The layout knows
            // which line a piece landed on and the measurement knows that line's width.
            foreach (var fragment in fragments)
            {
                if (fragment.Content == " ") continue;   // a space paints nothing
                // Start: a piece is one word rasterized ALONE, and the LINE it sits on is what
                // gets aligned — below, on the piece's x.
                var raster = cache.Get(runRasterizer, fragment.Content, fragment.Style, motion.TypeScale,
                    float.PositiveInfinity, 1, motion.RenderScale, TextAlignment.Start);
                if (raster is null) continue;
                var rect = new Rect(
                    node.Bounds.X + fragment.X + RichTextRuns.Shift(node, text, fragment),
                    node.Bounds.Y + fragment.Y - raster.PadTop / motion.RenderScale,
                    raster.Texture.Width / motion.RenderScale,
                    raster.Texture.Height / motion.RenderScale);
                var ink = fragment.Color is { } runColor ? runColor.Resolve(mode) : baseColor;
                builder.Texture(rect, ink, raster.Texture);
            }
            return;
        }

        if (motion.TextRasterizer is { } rasterizer && node.Text is { } measured)
        {
            var style = text.Resolve(theme);
        if (text.Italic) style = style with { Italic = true };
            var raster = (motion.TextCache ?? TextRasterCache.Shared).Get(
                rasterizer, text.PlainContent, style, motion.TypeScale, node.Bounds.Width, text.MaxLines,
                motion.RenderScale, text.Align);
            if (raster is not null)
            {
                // The bitmap may carry ink ABOVE the line box (a tall ascender, an accent); it
                // was grown upward, so the draw rises by the same amount and the line box itself
                // still sits exactly where layout put it.
                var rect = new Rect(node.Bounds.X, node.Bounds.Y - raster.PadTop / motion.RenderScale,
                    raster.Texture.Width / motion.RenderScale, raster.Texture.Height / motion.RenderScale);
                if (text.Gradient is { } g)
                {
                    // Gradient text: the glyph coverage is tinted by a PAINT. The axis spans the
                    // text's own box on the declared direction, matching what `background-clip:text`
                    // does on web (the gradient box is the element).
                    var end = g.Direction switch
                    {
                        GradientDirection.ToBottom => new Point(rect.X, rect.Y + rect.Height),
                        GradientDirection.ToBottomRight => new Point(rect.X + rect.Width, rect.Y + rect.Height),
                        GradientDirection.ToBottomLeft => new Point(rect.X, rect.Y + rect.Height),
                        _ => new Point(rect.X + rect.Width, rect.Y),
                    };
                    var start = g.Direction == GradientDirection.ToBottomLeft
                        ? new Point(rect.X + rect.Width, rect.Y)
                        : new Point(rect.X, rect.Y);
                    builder.Texture(rect,
                        Paint.Linear(start, end, g.From.Resolve(mode), g.To.Resolve(mode)),
                        raster.Texture);
                    return;
                }

                var color = (text.Color ?? theme.TextPrimary).Resolve(mode);
                builder.Texture(rect, color, raster.Texture);
                return;
            }
        }
        EmitTextPlaceholder(node, text, theme, mode, builder);
    }

    private static void EmitTextPlaceholder(LayoutNode node, Text text, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (node.Text is not { } measurement) return;
        var color = (text.Color ?? theme.TextPrimary).Resolve(mode).WithOpacity(0.30f);
        var barHeight = measurement.LineHeight * 0.55f;

        for (var i = 0; i < measurement.Lines.Count; i++)
        {
            var line = measurement.Lines[i];
            if (line.Width <= 0) continue;
            var y = node.Bounds.Y + i * measurement.LineHeight + (measurement.LineHeight - barHeight) / 2;
            // The bars align too. They stand in for the glyphs on a frame with no platform text
            // service, which is every golden test — so this is where alignment is ASSERTABLE
            // without a Mac, an emulator or a Windows box in the loop.
            var barWidth = MathF.Min(line.Width, node.Bounds.Width);
            builder.FillRRect(
                new RRect(new Rect(node.Bounds.X + text.Align.Offset(node.Bounds.Width, barWidth), y,
                        barWidth, barHeight),
                    new CornerRadii(barHeight / 3)),
                Paint.Solid(color));
        }
    }

    /// <summary>The TextEntry stand-in (spec B9 fence): one soft bar per the W4 text placeholder
    /// convention — the VALUE in the entry's text color, an empty value shows the PLACEHOLDER in
    /// TextMuted. Deterministic layout geometry until the real text stack (M4).</summary>
    /// <summary>
    /// An editable field: its text, its caret, and the region that makes it clickable.
    /// <para>
    /// The bar this used to draw was the W4 fence, kept long after the fence came down — every other
    /// string on screen was already rastering real glyphs. A field that shows a grey bar instead of
    /// what you typed is not a placeholder for a missing feature; it is a field nobody can use.
    /// </para>
    /// </summary>
    private static void EmitEntry(LayoutNode node, TextEntry entry, IAppTheme theme, ThemeMode mode,
        DisplayListBuilder builder, InputSink input, PressScope press, MotionScope motion)
    {
        // Clickable even when empty and even without a rasterizer: the region is where the caret
        // comes from, and an empty field is exactly the one you most need to click into.
        if (!entry.Disabled) input.Add(new TextRegion(node.Bounds, entry, node.Path ?? ""));

        var editing = press.TextPath is { Length: > 0 } && node.Path == press.TextPath;
        // The composition in flight renders INSIDE the value at the caret — underlined below, caret
        // at its end — while the VALUE stays untouched until the platform commits. An obscured
        // field composes blind: echoing the composition would echo the secret.
        var marked = editing && !entry.Obscure ? press.MarkedText : "";
        var caretInValue = Math.Min(press.CaretIndex, entry.Value.Length);
        var composed = marked.Length > 0 ? entry.Value.Insert(caretInValue, marked) : entry.Value;
        var value = entry.Obscure ? new string('•', composed.Length) : composed;
        var effectiveCaret = Math.Min(caretInValue + marked.Length, value.Length);
        var shown = value.Length > 0 ? value : entry.Placeholder ?? "";
        var token = value.Length > 0 ? theme.TextPrimary : theme.TextMuted;
        var style = theme.Type(entry.Role);

        var advance = 0f;
        var shift = 0f;
        if (motion.TextRasterizer is null)
        {
            // No platform text service — headless tests, and any surface where glyphs are not
            // available yet. The soft bar is the same stand-in `Text` falls back to; the caret still
            // draws, because where it is remains the useful thing to see.
            EmitEntryPlaceholder(node, entry, theme, mode, builder);
        }
        else if (shown.Length > 0)
        {
            var rasterizer = motion.TextRasterizer;
            // While EDITING, the raster is unbounded and the FIELD is the window onto it — the
            // browser's own input behaviour. Bounded-and-ellipsized is for reading, and an ellipsis
            // in a field someone is typing into hides exactly the characters they just typed.
            // Start: a field is not a paragraph, and the caret arithmetic below reads the raster's
            // WIDTH as the text's width — an aligned one is padded and would lie about it.
            var raster = (motion.TextCache ?? TextRasterCache.Shared)
                .Get(rasterizer, shown, style, motion.TypeScale,
                    editing ? float.MaxValue : node.Bounds.Width, 1, motion.RenderScale, TextAlignment.Start);
            if (raster is not null)
            {
                var width = raster.Texture.Width / motion.RenderScale;
                // Where the caret goes is a measurement of the text BEFORE it, not a fraction of the
                // whole: proportional glyphs make "iii" and "WWW" different widths at equal length.
                advance = effectiveCaret >= value.Length
                    ? (value.Length > 0 ? width : 0)
                    : MeasureUpTo(rasterizer, value, effectiveCaret, style, motion);

                // The window FOLLOWS the caret: when it would leave the right edge, the text slides
                // left just enough to keep it visible (a small margin shows the character being
                // approached). Derived from the caret alone — no scroll state to desynchronize.
                if (editing && advance > node.Bounds.Width - CaretFollowMargin)
                    shift = advance - (node.Bounds.Width - CaretFollowMargin);

                var rect = new Rect(node.Bounds.X - shift, node.Bounds.Y,
                    width, raster.Texture.Height / motion.RenderScale);
                if (shift > 0 || width > node.Bounds.Width)
                {
                    builder.PushClip(new RRect(node.Bounds, default));
                    builder.Texture(rect, token.Resolve(mode), raster.Texture);
                    builder.PopClip();
                }
                else
                {
                    builder.Texture(rect, token.Resolve(mode), raster.Texture);
                }
            }
        }

        if (!editing || entry.Disabled) return;

        var caretHeight = node.Text?.LineHeight ?? style.LineHeight;

        // The composition's underline — the one visual that says "this text is not yours yet".
        if (marked.Length > 0 && motion.TextRasterizer is { } markedRasterizer)
        {
            var from = MeasureUpTo(markedRasterizer, value, caretInValue, style, motion) - shift;
            var to = MeasureUpTo(markedRasterizer, value, caretInValue + marked.Length, style, motion) - shift;
            from = MathF.Max(from, 0);
            to = MathF.Min(to, node.Bounds.Width);
            if (to > from)
                builder.FillRRect(
                    new RRect(new Rect(node.Bounds.X + from, node.Bounds.Y + caretHeight - 2f, to - from, 1.5f),
                        new CornerRadii(0.75f)),
                    Paint.Solid(theme.TextPrimary.Resolve(mode)));
        }

        // The selection band. Drawn AFTER the glyphs and translucent rather than under them and
        // opaque: the text stays legible through it, which is what every platform does, and it
        // saves measuring the run twice to paint around it.
        if (press.SelectionEnd > press.SelectionStart && motion.TextRasterizer is { } selectionRasterizer)
        {
            var from = MeasureUpTo(selectionRasterizer, value, press.SelectionStart, style, motion) - shift;
            var to = MeasureUpTo(selectionRasterizer, value, press.SelectionEnd, style, motion) - shift;
            from = MathF.Max(from, 0);
            to = MathF.Min(to, node.Bounds.Width);
            if (to > from)
                builder.FillRRect(
                    new RRect(new Rect(node.Bounds.X + from, node.Bounds.Y, to - from, caretHeight),
                        new CornerRadii(1)),
                    Paint.Solid(theme.FocusRing.Resolve(mode).WithOpacity(0.28f)));
        }

        // The caret is drawn WITH the selection, not instead of it. The band says which characters
        // are held; the caret says which END you are holding — the one ⇧→ will move, the one the
        // next character replaces from. Hiding it is why a selection used to feel directionless.
        if (!press.CaretVisible) return;
        builder.FillRRect(
            new RRect(new Rect(node.Bounds.X + advance - shift, node.Bounds.Y, CaretWidth, caretHeight),
                new CornerRadii(0)),
            Paint.Solid(theme.TextPrimary.Resolve(mode)));
    }

    private static void EmitEntryPlaceholder(LayoutNode node, TextEntry entry, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder)
    {
        if (node.Text is not { } measurement || measurement.Lines.Count == 0) return;
        var hasValue = entry.Value.Length > 0;
        var token = hasValue ? theme.TextPrimary : theme.TextMuted;
        var color = token.Resolve(mode).WithOpacity(0.30f);
        var line = measurement.Lines[0];
        if (line.Width <= 0) return;

        var barHeight = measurement.LineHeight * 0.55f;
        var y = node.Bounds.Y + (measurement.LineHeight - barHeight) / 2;
        builder.FillRRect(
            new RRect(new Rect(node.Bounds.X, y, MathF.Min(line.Width, node.Bounds.Width), barHeight),
                new CornerRadii(barHeight / 3)),
            Paint.Solid(color));
    }

    /// <summary>
    /// The carets and the selection over an editable code surface. The child drew the code; these are
    /// the marks that say where you are in it.
    /// <para>
    /// Painted, never computed: the MODEL answers where every band and caret goes, in the surface's
    /// own coordinates, and this only offsets them by where the surface landed. The arithmetic from a
    /// (line, column) to a point used to be here and again in the web lowering, and a caret that two
    /// hosts place separately is a caret that ends up in two places.
    /// </para>
    /// </summary>
    private static void EmitCodeSurface(LayoutNode node, CodeSurface surface, IAppTheme theme,
        ThemeMode mode, DisplayListBuilder builder, InputSink input, PressScope press, MotionScope motion)
    {
        input.Add(new CodeRegion(node.Bounds, surface, node.Path ?? ""));

        // Drawn BEFORE the child, so the code paints over the marks: a translucent band keeps the
        // text legible through it, and a caret sits BETWEEN glyphs, where nothing occludes it.
        var editing = press.TextPath is { Length: > 0 } && node.Path == press.TextPath;
        if (!editing) return;

        var model = surface.Model;
        var left = node.Bounds.X;
        var top = node.Bounds.Y;

        var bands = model.SelectionBands;
        if (bands.Count > 0)
        {
            var paint = Paint.Solid((surface.SelectionColor ?? theme.FocusRing).Resolve(mode).WithOpacity(SelectionAlpha));
            for (var i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                builder.FillRRect(new RRect(new Rect(left + band.X, top + band.Y, band.Width, band.Height),
                    new CornerRadii(1)), paint);
            }
        }

        // The carets sit at each selection's FOCUS end, drawn with the band and not instead of it: the
        // band says which characters are held, a caret says which end ⇧-arrow moves.
        if (!press.CaretVisible) return;
        var ink = Paint.Solid((surface.CaretColor ?? theme.TextPrimary).Resolve(mode));
        var carets = model.Carets;
        for (var i = 0; i < carets.Count; i++)
        {
            var caret = carets[i];
            builder.FillRRect(new RRect(new Rect(left + caret.X, top + caret.Y, caret.Width, caret.Height),
                new CornerRadii(0)), ink);
        }
    }

    /// <summary>The width of the first <paramref name="count"/> characters — the caret's x.</summary>
    private static float MeasureUpTo(Framework.ITextRasterizer rasterizer, string value, int count,
        TypeStyle style, MotionScope motion)
    {
        if (count <= 0) return 0;
        var raster = (motion.TextCache ?? TextRasterCache.Shared).Get(
            rasterizer, value[..Math.Min(count, value.Length)], style, motion.TypeScale, float.MaxValue, 1,
            motion.RenderScale, TextAlignment.Start);   // a PREFIX's width: padding it would move the caret
        return raster is null ? 0 : raster.Texture.Width / motion.RenderScale;
    }
}
