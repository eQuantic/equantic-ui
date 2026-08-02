using System.Runtime.InteropServices;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The W4 icon rasterizer on macOS — CoreGraphics only (the zero third-party rule): the shared
/// <see cref="SvgPath"/> parser lowers the glyph's path data to moves/lines/cubics, CG fills
/// (nonzero) or strokes (round caps/joins, the icon-pack convention) into an A8 coverage bitmap at
/// device scale. The CTM flips CG's bottom-up frame and scales viewBox units → pixels, so stroke
/// widths stay in glyph units. v1 fences: single fill rule (nonzero — IconGlyph carries none),
/// per-glyph raster (no atlas packing yet).
/// </summary>
public sealed partial class CoreGraphicsIconRasterizer : IIconRasterizer
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGBitmapContextCreate(IntPtr data, nuint width, nuint height,
        nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGBitmapContextGetData(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextRelease(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextScaleCTM(IntPtr context, double sx, double sy);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextTranslateCTM(IntPtr context, double tx, double ty);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextBeginPath(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextMoveToPoint(IntPtr context, double x, double y);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextAddLineToPoint(IntPtr context, double x, double y);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextAddCurveToPoint(IntPtr context,
        double c1x, double c1y, double c2x, double c2y, double x, double y);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextClosePath(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextFillPath(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextStrokePath(IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextSetLineWidth(IntPtr context, double width);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextSetLineCap(IntPtr context, int cap);

    [LibraryImport(CoreGraphics)]
    private static partial void CGContextSetLineJoin(IntPtr context, int join);

    public TextRaster? Rasterize(IconGlyph glyph, float sizeDp, float scale)
    {
        var segments = SvgPath.Parse(glyph.Path);
        if (segments.Count == 0) return null;

        // viewBox "minX minY w h" → normalize to origin, scale units → pixels.
        var parts = glyph.ViewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !float.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var minX)
            || !float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var minY)
            || !float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out var unitW)
            || !float.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, out var unitH)
            || unitW <= 0 || unitH <= 0) return null;

        var px = Math.Max(1, (int)MathF.Ceiling(sizeDp * scale));
        var context = CGBitmapContextCreate(IntPtr.Zero, (nuint)px, (nuint)px,
            8, (nuint)px, IntPtr.Zero, 7 /* kCGImageAlphaOnly */);
        if (context == IntPtr.Zero) return null;
        try
        {
            // Flip to top-down, then viewBox units → pixels.
            CGContextTranslateCTM(context, 0, px);
            CGContextScaleCTM(context, px / (double)unitW, -(px / (double)unitH));
            CGContextTranslateCTM(context, -minX, -minY);

            CGContextBeginPath(context);
            foreach (var segment in segments)
            {
                switch (segment.Verb)
                {
                    case PathVerb.Move:
                        CGContextMoveToPoint(context, segment.End.X, segment.End.Y);
                        break;
                    case PathVerb.Line:
                        CGContextAddLineToPoint(context, segment.End.X, segment.End.Y);
                        break;
                    case PathVerb.Cubic:
                        CGContextAddCurveToPoint(context,
                            segment.C1.X, segment.C1.Y, segment.C2.X, segment.C2.Y,
                            segment.End.X, segment.End.Y);
                        break;
                    case PathVerb.Close:
                        CGContextClosePath(context);
                        break;
                }
            }

            if (glyph.Style == IconGlyphStyle.Stroke)
            {
                CGContextSetLineWidth(context, glyph.StrokeWidth);
                CGContextSetLineCap(context, 1);  // round — the icon-pack convention
                CGContextSetLineJoin(context, 1); // round
                CGContextStrokePath(context);
            }
            else
            {
                CGContextFillPath(context);
            }

            var data = CGBitmapContextGetData(context);
            if (data == IntPtr.Zero) return null;
            var alpha = new byte[px * px];
            Marshal.Copy(data, alpha, 0, alpha.Length);
            return new TextRaster(px, px, alpha);
        }
        finally
        {
            CGContextRelease(context);
        }
    }
}
