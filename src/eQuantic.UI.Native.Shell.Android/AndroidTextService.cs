using Android.Graphics;
using Android.Text;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
// Both the platform and the vocabulary have a Color. Here the platform's is the one being set on a
// platform Paint; the engine's never reaches this file, because a coverage raster carries no colour.
using Color = Android.Graphics.Color;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Text on Android, through the platform's own shaper — a system framework, like Metal and CoreText
/// are on Apple, and no third party. ONE service measures and rasterizes, because a layout broken
/// by one set of metrics and drawn with another is a layout that disagrees with itself.
/// <para>
/// <see cref="StaticLayout"/> decides where the lines BREAK, which is the part a shaper must own.
/// Where they SIT is the design system's decision: each line is drawn on the style's line-height
/// grid rather than on the font's, which is the same contract CoreText is held to.
/// </para>
/// </summary>
public sealed class AndroidTextService : ITextMeasurer, ITextRasterizer
{
    private readonly Dictionary<(float Size, FontWeight Weight), TextPaint> _paints = new();

    public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var lineHeight = style.ScaledLineHeight(typeScale);
        var lines = Break(content, style, typeScale, maxWidth, maxLines);
        var width = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
        return new TextMeasurement(width, lines.Count * lineHeight, lineHeight,
            lines.Select(line => new MeasuredLine(line.Width, line.Ellipsized)).ToArray());
    }

    public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines, float scale)
    {
        if (content.Length == 0) return null;

        var lineHeight = style.ScaledLineHeight(typeScale);
        var lines = Break(content, style, typeScale, maxWidth, maxLines);
        if (lines.Count == 0) return null;

        var paint = PaintFor(style.ScaledSize(typeScale) * scale, style.Weight);
        // Measured with the SCALED paint, because a glyph's advance is not exactly linear in its
        // size: a width taken at 1x and multiplied comes out a hair short, and the last character
        // of every line is what gets sliced off.
        var width = (int)MathF.Ceiling(lines.Max(line => paint.MeasureText(line.Text)));
        var height = (int)MathF.Ceiling(lines.Count * lineHeight * scale);
        if (width <= 0 || height <= 0) return null;
        var metrics = paint.GetFontMetrics()!;
        // The baseline within each line box: the STYLE's grid, with the font centred inside it.
        var baseline = (lineHeight * scale - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;

        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        paint.Color = Color.White;
        for (var i = 0; i < lines.Count; i++)
            canvas.DrawText(lines[i].Text, 0, i * lineHeight * scale + baseline, paint);

        // A8 COVERAGE is what the engine's textured pipeline samples: the tint belongs to the draw
        // command, so one raster serves light and dark alike.
        var argb = new int[width * height];
        bitmap.GetPixels(argb, 0, width, 0, 0, width, height);
        var alpha = new byte[argb.Length];
        for (var i = 0; i < argb.Length; i++) alpha[i] = (byte)((argb[i] >> 24) & 0xFF);

        return new TextRaster(width, height, alpha);
    }

    private readonly record struct Line(string Text, float Width, bool Ellipsized);

    /// <summary>Where the lines break, and what each one ends up saying.</summary>
    private List<Line> Break(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var paint = PaintFor(style.ScaledSize(typeScale), style.Weight);
        // An unconstrained paragraph still needs a number; the text's own width is the smallest one
        // that changes nothing.
        var wrapAt = float.IsPositiveInfinity(maxWidth)
            ? (int)MathF.Ceiling(paint.MeasureText(content)) + 1
            : Math.Max(1, (int)MathF.Floor(maxWidth));

        var builder = StaticLayout.Builder.Obtain(content, 0, content.Length, paint, wrapAt)!
            .SetIncludePad(false)!;
        if (maxLines > 0) builder = builder.SetMaxLines(maxLines)!.SetEllipsize(TextUtils.TruncateAt.End)!;

        using var layout = builder.Build()!;
        var lines = new List<Line>(layout.LineCount);
        for (var i = 0; i < layout.LineCount; i++)
        {
            var text = content[layout.GetLineStart(i)..layout.GetLineEnd(i)].TrimEnd('\n');
            var ellipsized = layout.GetEllipsisCount(i) > 0;
            if (ellipsized)
            {
                // The layout says how much it dropped; the glyph it would have drawn is ours.
                var start = Math.Clamp(layout.GetEllipsisStart(i), 0, text.Length);
                text = text[..start].TrimEnd() + "…";
            }
            lines.Add(new Line(text, paint.MeasureText(text), ellipsized));
        }

        return lines;
    }

    private TextPaint PaintFor(float size, FontWeight weight)
    {
        if (_paints.TryGetValue((size, weight), out var cached)) return cached;

        var paint = new TextPaint(PaintFlags.AntiAlias | PaintFlags.SubpixelText)
        {
            TextSize = size,
            TextAlign = Paint.Align.Left,
        };
        paint.SetTypeface(Typeface.Create(Typeface.Default,
            weight >= FontWeight.SemiBold ? TypefaceStyle.Bold : TypefaceStyle.Normal));
        _paints[(size, weight)] = paint;
        return paint;
    }
}
