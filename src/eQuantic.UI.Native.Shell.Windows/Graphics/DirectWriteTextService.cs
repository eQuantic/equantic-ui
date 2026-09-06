using System.Globalization;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// The W4 text service on Windows — DirectWrite for layout and Direct2D for the raster, SYSTEM
/// components only (the zero third-party rule, as CoreText is on the Mac): ONE engine serves both
/// <see cref="ITextMeasurer"/> (layout line breaks) and <see cref="ITextRasterizer"/> (A8 coverage
/// rasters), so breaks agree by construction. Lines sit on the STYLE's line-height grid — uniform
/// line spacing with the font's ascent+descent centred in it, the same arithmetic the Mac does by
/// hand — and the raster draws through a scaled transform, so wrapping happens in dp while pixels
/// come out at device scale.
/// <para>
/// The face is the system's: <c>Segoe UI</c> for text, <c>Cascadia Mono</c> — Windows 11's own
/// monospaced face — for code, falling back to <c>Consolas</c> where Cascadia is not installed. A
/// weight is asked for as the number it is (400, 600, 700): DirectWrite picks the nearest face the
/// family has, which for Segoe UI is a real cut at every one of them.
/// </para>
/// <para>
/// Coverage is LINEAR — custom rendering params with gamma 1.0 and no contrast enhancement — because
/// the engine tints and blends it in linear light itself, and CoreText's alpha-only contexts hand
/// over the same thing. The default parameters would bake a display gamma into the coverage and
/// every glyph would come out heavier on Windows than on the Mac for no reason anyone chose.
/// </para>
/// v1 fences, shared with the Mac: no trailing ellipsis on truncation (measure reports the cut),
/// per-process format cache.
/// </summary>
public sealed unsafe class DirectWriteTextService : ITextMeasurer, ITextRasterizer, IDisposable
{
    private readonly void* _factory;          // IDWriteFactory
    private readonly void* _d2d;              // ID2D1Factory
    private readonly void* _wic;              // IWICImagingFactory
    private readonly void* _collection;       // IDWriteFontCollection (system)
    private readonly void* _renderingParams;  // IDWriteRenderingParams (linear coverage)
    private readonly string _textFamily;
    private readonly string _monoFamily;
    private readonly string _locale;
    private readonly Dictionary<FormatKey, Format> _formats = new();
    private bool _disposed;

    private readonly record struct FormatKey(float Size, float LineHeight, FontWeight Weight, bool Mono, bool Italic);

    /// <summary>A text format and the metrics of the face it resolved to, in dp at its size.</summary>
    private sealed unsafe class Format
    {
        public void* Handle;
        public float Ascent;
        public float Descent;
    }

    public DirectWriteTextService()
    {
        Com.EnsureInitialized();
        _factory = DWrite.CreateFactory();
        _d2d = D2D.CreateFactory();
        _wic = Wic.CreateFactory();

        void* collection;
        Com.Check(DWrite.GetSystemFontCollection(_factory, &collection), "system font collection");
        _collection = collection;

        _textFamily = FirstInstalled("Segoe UI") ?? FirstInstalled("Arial") ?? "Segoe UI";
        _monoFamily = FirstInstalled("Cascadia Mono") ?? FirstInstalled("Consolas") ?? _textFamily;
        _locale = CultureInfo.CurrentUICulture.Name is { Length: > 0 } name ? name : "en-us";

        void* renderingParams;
        Com.Check(DWrite.CreateCustomRenderingParams(_factory, 1.0f, 0f, 0f, DWrite.PixelGeometryFlat,
            DWrite.RenderingModeNaturalSymmetric, &renderingParams), "rendering params");
        _renderingParams = renderingParams;
    }

    /// <summary>The family the system's own UI is set in, and the monospaced one beside it.</summary>
    public string TextFamily => _textFamily;

    public string MonoFamily => _monoFamily;

    private string? FirstInstalled(string family)
    {
        uint index;
        int exists;
        fixed (char* name = family)
        {
            if (DWrite.FindFamilyName(_collection, name, &index, &exists) < 0) return null;
        }
        return exists != 0 ? family : null;
    }

    private Format FormatFor(float size, float lineHeight, FontWeight weight, bool mono, bool italic)
    {
        var key = new FormatKey(size, lineHeight, weight, mono, italic);
        if (_formats.TryGetValue(key, out var cached)) return cached;

        var family = mono ? _monoFamily : _textFamily;
        var style = italic ? DWrite.StyleItalic : DWrite.StyleNormal;
        void* format;
        fixed (char* familyName = family)
        fixed (char* locale = _locale)
        {
            Com.Check(DWrite.CreateTextFormat(_factory, familyName, null, (int)weight, style,
                DWrite.StretchNormal, size, locale, &format), "text format creation");
        }
        Com.Check(DWrite.SetWordWrapping(format, DWrite.WordWrappingWrap), "word wrapping");

        // The face's ascent and descent at this size, so the line box centres the glyph run on
        // OUR grid exactly as the Mac does — layout and raster agree on where a baseline is.
        var (ascent, descent) = FaceMetrics(family, weight, style, size);
        var baseline = (lineHeight - (ascent + descent)) / 2 + ascent;
        Com.Check(DWrite.SetLineSpacing(format, DWrite.LineSpacingUniform, lineHeight, baseline), "line spacing");

        var created = new Format { Handle = format, Ascent = ascent, Descent = descent };
        _formats[key] = created;   // per-process cache (the documented lifetime fence)
        return created;
    }

    private (float Ascent, float Descent) FaceMetrics(string family, FontWeight weight, uint style, float size)
    {
        void* fontFamily = null;
        void* font = null;
        try
        {
            uint index;
            int exists;
            fixed (char* name = family)
            {
                if (DWrite.FindFamilyName(_collection, name, &index, &exists) < 0 || exists == 0)
                    return (size * 0.8f, size * 0.2f);
            }
            if (DWrite.GetFontFamily(_collection, index, &fontFamily) < 0) return (size * 0.8f, size * 0.2f);
            if (DWrite.GetFirstMatchingFont(fontFamily, (int)weight, DWrite.StretchNormal, style, &font) < 0)
                return (size * 0.8f, size * 0.2f);

            DWrite.FontMetrics metrics;
            DWrite.GetMetrics(font, &metrics);
            if (metrics.DesignUnitsPerEm == 0) return (size * 0.8f, size * 0.2f);
            var unit = size / metrics.DesignUnitsPerEm;
            return (metrics.Ascent * unit, metrics.Descent * unit);
        }
        finally
        {
            Com.Release(ref font);
            Com.Release(ref fontFamily);
        }
    }

    /// <summary>A laid-out block (caller releases). Unconstrained width is a very wide box: the
    /// wrap mode needs a number, and DirectWrite refuses infinity.</summary>
    private void* Layout(string content, Format format, float maxWidth, float maxHeight)
    {
        var width = float.IsFinite(maxWidth) && maxWidth > 0 ? maxWidth : 100_000f;
        void* layout;
        fixed (char* text = content)
        {
            Com.Check(DWrite.CreateTextLayout(_factory, text, (uint)content.Length, format.Handle,
                width, maxHeight, &layout), "text layout creation");
        }
        return layout;
    }

    /// <summary>
    /// Every line's width WITHOUT its trailing whitespace, and its length in code units. DirectWrite
    /// gives the layout's ink width as one number and each line's length but not its width, so the
    /// widths are summed from the cluster metrics line by line — the same clusters it broke on.
    /// </summary>
    private static (float Width, int Length)[] Lines(void* layout)
    {
        uint lineCount = 0;
        DWrite.GetLineMetrics(layout, null, 0, &lineCount);
        if (lineCount == 0) return [];
        var lines = new DWrite.LineMetrics[lineCount];
        fixed (DWrite.LineMetrics* linePtr = lines)
            Com.Check(DWrite.GetLineMetrics(layout, linePtr, lineCount, &lineCount), "line metrics");

        uint clusterCount = 0;
        DWrite.GetClusterMetrics(layout, null, 0, &clusterCount);
        var clusters = new DWrite.ClusterMetrics[Math.Max(1, clusterCount)];
        if (clusterCount > 0)
        {
            fixed (DWrite.ClusterMetrics* clusterPtr = clusters)
                Com.Check(DWrite.GetClusterMetrics(layout, clusterPtr, clusterCount, &clusterCount), "cluster metrics");
        }

        var result = new (float Width, int Length)[lineCount];
        var cluster = 0;
        for (var line = 0; line < lineCount; line++)
        {
            var remaining = (int)lines[line].Length;
            var advance = 0f;
            var ink = 0f;
            while (remaining > 0 && cluster < clusterCount)
            {
                var c = clusters[cluster++];
                remaining -= c.Length;
                advance += c.Width;
                if (!c.IsWhitespace && !c.IsNewline) ink = advance;
            }
            result[line] = (ink, (int)lines[line].Length);
        }
        return result;
    }

    public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var size = style.ScaledSize(typeScale);
        var lineHeight = style.ScaledLineHeight(typeScale);
        if (content.Length == 0)
            return new TextMeasurement(0, lineHeight, lineHeight, [new MeasuredLine(0, false)]);

        var format = FormatFor(size, lineHeight, style.Weight, style.Mono, style.Italic);
        var layout = Layout(content, format, maxWidth, 1_000_000f);
        try
        {
            var lines = Lines(layout);
            var shown = maxLines > 0 ? Math.Min(lines.Length, maxLines) : lines.Length;
            var measured = new List<MeasuredLine>(Math.Max(1, shown));
            var maxLineWidth = 0f;
            for (var i = 0; i < shown; i++)
            {
                measured.Add(new MeasuredLine(lines[i].Width, Ellipsized: i == shown - 1 && shown < lines.Length));
                maxLineWidth = MathF.Max(maxLineWidth, lines[i].Width);
            }
            if (measured.Count == 0) measured.Add(new MeasuredLine(0, false));
            return new TextMeasurement(maxLineWidth, measured.Count * lineHeight, lineHeight, measured);
        }
        finally
        {
            Com.Release(layout);
        }
    }

    public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines, float scale)
    {
        if (content.Length == 0) return null;
        var size = style.ScaledSize(typeScale);
        var lineHeight = style.ScaledLineHeight(typeScale);
        var format = FormatFor(size, lineHeight, style.Weight, style.Mono, style.Italic);

        var layout = Layout(content, format, maxWidth, 1_000_000f);
        try
        {
            var lines = Lines(layout);
            var count = lines.Length;
            var shown = maxLines > 0 ? Math.Min(count, maxLines) : count;
            if (shown == 0) return null;

            // A cut block is laid out AGAIN from the text that survives the cut: the lines past it
            // must not exist, or their ascenders would draw into the pad the last line's descender
            // needs. The Mac draws only the shown CTLines; this is the same decision.
            if (shown < count)
            {
                var kept = 0;
                for (var i = 0; i < shown; i++) kept += lines[i].Length;
                Com.Release(layout);
                layout = null;
                layout = Layout(content[..Math.Min(kept, content.Length)], format, maxWidth, 1_000_000f);
                lines = Lines(layout);
                shown = Math.Min(shown, lines.Length);
                if (shown == 0) return null;
            }

            var widthDp = 0f;
            for (var i = 0; i < shown; i++) widthDp = MathF.Max(widthDp, lines[i].Width);

            // The line box is the LAYOUT's; the ink is the FONT's, and it does not always fit — a
            // deep descender, an accent in any script. The overhang metrics say how far the ink
            // passes the box on each side, once the box is told how tall it is.
            Com.Check(DWrite.SetMaxHeight(layout, shown * lineHeight), "layout height");
            DWrite.OverhangMetrics overhang;
            Com.Check(DWrite.GetOverhangMetrics(layout, &overhang), "overhang metrics");

            // ONE DP of guard on each side, not one pixel: antialiasing spreads in device pixels and
            // a dp is the unit the rest of the geometry speaks, so the margin holds at any scale.
            var guardPx = (int)MathF.Ceiling(scale);
            var padTopPx = (int)MathF.Ceiling(MathF.Max(0, overhang.Top) * scale) + guardPx;
            var padDp = padTopPx / scale;
            var inkBottom = shown * lineHeight + MathF.Max(0, overhang.Bottom);

            var pxWidth = Math.Max(1, (int)MathF.Ceiling(widthDp * scale));
            var pxHeight = Math.Max(1,
                (int)MathF.Ceiling(MathF.Max(padDp + inkBottom, shown * lineHeight) * scale)) + guardPx;

            var drawn = layout;
            return CoverageCanvas.Draw(_d2d, _wic, pxWidth, pxHeight, padTopPx, (target, brush) =>
            {
                var t = (void*)target;
                D2D.SetTextAntialiasMode(t, D2D.TextAntialiasModeGrayscale);
                D2D.SetTextRenderingParams(t, _renderingParams);
                var transform = D2D.Matrix3x2.Scale(scale, scale);
                D2D.SetTransform(t, &transform);
                D2D.DrawTextLayout(t, new D2D.Point2F(0, padDp), drawn, (void*)brush, D2D.DrawTextOptionsNone);
            });
        }
        finally
        {
            if (layout is not null) Com.Release(layout);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var format in _formats.Values) Com.Release(format.Handle);
        _formats.Clear();
        Com.Release(_renderingParams);
        Com.Release(_collection);
        Com.Release(_wic);
        Com.Release(_d2d);
        Com.Release(_factory);
    }
}
