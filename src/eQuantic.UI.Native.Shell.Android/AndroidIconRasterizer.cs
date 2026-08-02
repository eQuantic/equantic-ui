using Android.Graphics;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using Color = Android.Graphics.Color;
using Path = Android.Graphics.Path;
// The vocabulary and the platform both have a PathVerb; the shared parser's is the one being read.
using PathVerb = eQuantic.UI.Native.Framework.PathVerb;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Icons on Android, through the platform's own path renderer — the same shared
/// <see cref="SvgPath"/> parser the Apple rasterizer uses, lowered onto <see cref="Path"/> instead
/// of CoreGraphics. Same glyphs, same units, same A8 coverage; only the fill differs, which is
/// exactly the split the engine was built around.
/// </summary>
public sealed class AndroidIconRasterizer : IIconRasterizer
{
    public TextRaster? Rasterize(IconGlyph glyph, float sizeDp, float scale)
    {
        var segments = SvgPath.Parse(glyph.Path);
        if (segments.Count == 0) return null;

        var parts = glyph.ViewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !float.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var minX)
            || !float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var minY)
            || !float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var unitW)
            || !float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var unitH)
            || unitW <= 0 || unitH <= 0) return null;

        var px = Math.Max(1, (int)MathF.Ceiling(sizeDp * scale));

        using var path = new Path();
        foreach (var segment in segments)
        {
            switch (segment.Verb)
            {
                case PathVerb.Move:
                    path.MoveTo(segment.End.X, segment.End.Y);
                    break;
                case PathVerb.Line:
                    path.LineTo(segment.End.X, segment.End.Y);
                    break;
                case PathVerb.Cubic:
                    path.CubicTo(segment.C1.X, segment.C1.Y, segment.C2.X, segment.C2.Y,
                        segment.End.X, segment.End.Y);
                    break;
                case PathVerb.Close:
                    path.Close();
                    break;
            }
        }

        using var paint = new Paint(PaintFlags.AntiAlias) { Color = Color.White };
        if (glyph.Style == IconGlyphStyle.Stroke)
        {
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeWidth = glyph.StrokeWidth;
            // Round caps and joins: the icon packs' own convention, and what keeps a 2dp stroke
            // from growing spikes where two segments meet at a sharp angle.
            paint.StrokeCap = Paint.Cap.Round;
            paint.StrokeJoin = Paint.Join.Round;
        }
        else
        {
            paint.SetStyle(Paint.Style.Fill);
        }

        using var bitmap = Bitmap.CreateBitmap(px, px, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bitmap);
        // viewBox units → pixels, with the box's own origin taken out first.
        canvas.Scale(px / unitW, px / unitH);
        canvas.Translate(-minX, -minY);
        canvas.DrawPath(path, paint);

        var argb = new int[px * px];
        bitmap.GetPixels(argb, 0, px, 0, 0, px, px);
        var alpha = new byte[argb.Length];
        for (var i = 0; i < argb.Length; i++) alpha[i] = (byte)((argb[i] >> 24) & 0xFF);

        return new TextRaster(px, px, alpha);
    }
}
