using System.Runtime.InteropServices;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The W4 text service on macOS — CoreText/CoreGraphics, SYSTEM frameworks only (the zero
/// third-party rule): ONE engine serves both <see cref="ITextMeasurer"/> (layout line breaks) and
/// <see cref="ITextRasterizer"/> (A8 coverage rasters), so breaks agree by construction. Lines sit
/// on the STYLE's line-height grid (the layout contract), not the font's; the raster draws through
/// a scaled CTM, so wrapping happens in dp (identical to measuring) while pixels come out at
/// device scale. v1 fences: no trailing ellipsis on truncation (measure reports the cut), weight
/// maps to the bold trait at ≥600 only, per-process CTFont cache.
/// </summary>
public sealed partial class CoreTextService : ITextMeasurer, ITextRasterizer
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreTextLib = "/System/Library/Frameworks/CoreText.framework/CoreText";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const uint Utf8 = 0x08000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct CFRange
    {
        public long Location, Length;
    }

    [LibraryImport(CoreFoundation, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CFStringCreateWithCString(IntPtr alloc, string str, uint encoding);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(IntPtr cf);

    [LibraryImport(CoreFoundation)]
    private static partial IntPtr CFDictionaryCreateMutable(IntPtr alloc, long capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [LibraryImport(CoreFoundation)]
    private static partial void CFDictionarySetValue(IntPtr dict, IntPtr key, IntPtr value);

    [LibraryImport(CoreFoundation)]
    private static partial IntPtr CFAttributedStringCreate(IntPtr alloc, IntPtr str, IntPtr attributes);

    [LibraryImport(CoreFoundation)]
    private static partial long CFArrayGetCount(IntPtr array);

    [LibraryImport(CoreFoundation)]
    private static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);

    [LibraryImport(CoreTextLib)]
    private static partial IntPtr CTFontCreateUIFontForLanguage(uint uiType, double size, IntPtr language);

    [LibraryImport(CoreTextLib)]
    private static partial IntPtr CTFontCreateCopyWithSymbolicTraits(IntPtr font, double size, IntPtr matrix, uint traits, uint mask);

    [LibraryImport(CoreTextLib)]
    private static partial IntPtr CTFramesetterCreateWithAttributedString(IntPtr attributed);

    [LibraryImport(CoreTextLib)]
    private static partial IntPtr CTFramesetterCreateFrame(IntPtr framesetter, CFRange range, IntPtr path, IntPtr attributes);

    [LibraryImport(CoreTextLib)]
    private static partial IntPtr CTFrameGetLines(IntPtr frame);

    [LibraryImport(CoreTextLib)]
    private static partial double CTLineGetTypographicBounds(IntPtr line, out double ascent, out double descent, out double leading);

    [LibraryImport(CoreTextLib)]
    private static partial void CTLineDraw(IntPtr line, IntPtr context);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGPathCreateWithRect(CGRect rect, IntPtr transform);

    [LibraryImport(CoreGraphics)]
    private static partial IntPtr CGPathRelease(IntPtr path);

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
    private static partial void CGContextSetTextPosition(IntPtr context, double x, double y);

    [LibraryImport("/usr/lib/libSystem.B.dylib", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string path, int mode);

    [LibraryImport("/usr/lib/libSystem.B.dylib", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlsym(IntPtr handle, string symbol);

    private static readonly IntPtr FontAttributeName = LoadGlobal(CoreTextLib, "kCTFontAttributeName");
    private static readonly IntPtr KeyCallbacks = LoadGlobalAddress(CoreFoundation, "kCFTypeDictionaryKeyCallBacks");
    private static readonly IntPtr ValueCallbacks = LoadGlobalAddress(CoreFoundation, "kCFTypeDictionaryValueCallBacks");

    private static IntPtr LoadGlobal(string library, string symbol)
    {
        var address = LoadGlobalAddress(library, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    private static IntPtr LoadGlobalAddress(string library, string symbol) =>
        dlsym(dlopen(library, 2), symbol);

    private readonly Dictionary<(float Size, bool Bold), IntPtr> _fonts = new();

    private IntPtr FontFor(float size, FontWeight weight)
    {
        var bold = weight >= FontWeight.SemiBold;
        if (_fonts.TryGetValue((size, bold), out var cached)) return cached;

        var font = CTFontCreateUIFontForLanguage(2 /* kCTFontUIFontSystem */, size, IntPtr.Zero);
        if (bold)
        {
            const uint boldTrait = 1 << 1;
            var boldFont = CTFontCreateCopyWithSymbolicTraits(font, size, IntPtr.Zero, boldTrait, boldTrait);
            if (boldFont != IntPtr.Zero)
            {
                CFRelease(font);
                font = boldFont;
            }
        }
        _fonts[(size, bold)] = font; // per-process cache (the documented lifetime fence)
        return font;
    }

    /// <summary>The framesetter + laid-out frame for a block (caller releases all three handles).</summary>
    private (IntPtr Framesetter, IntPtr Frame, IntPtr Path, IntPtr Attributed, IntPtr CfText, IntPtr Dict)
        Layout(string content, float fontSize, FontWeight weight, float maxWidth)
    {
        var cfText = CFStringCreateWithCString(IntPtr.Zero, content.Length == 0 ? " " : content, Utf8);
        var dict = CFDictionaryCreateMutable(IntPtr.Zero, 1, KeyCallbacks, ValueCallbacks);
        CFDictionarySetValue(dict, FontAttributeName, FontFor(fontSize, weight));
        var attributed = CFAttributedStringCreate(IntPtr.Zero, cfText, dict);
        var framesetter = CTFramesetterCreateWithAttributedString(attributed);
        var width = float.IsFinite(maxWidth) && maxWidth > 0 ? maxWidth : 100_000f;
        var path = CGPathCreateWithRect(new CGRect(0, 0, width, 1_000_000), IntPtr.Zero);
        var frame = CTFramesetterCreateFrame(framesetter, default, path, IntPtr.Zero);
        return (framesetter, frame, path, attributed, cfText, dict);
    }

    private static void ReleaseLayout((IntPtr Framesetter, IntPtr Frame, IntPtr Path, IntPtr Attributed, IntPtr CfText, IntPtr Dict) l)
    {
        CFRelease(l.Frame);
        CGPathRelease(l.Path);
        CFRelease(l.Framesetter);
        CFRelease(l.Attributed);
        CFRelease(l.Dict);
        CFRelease(l.CfText);
    }

    public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var size = style.ScaledSize(typeScale);
        var lineHeight = style.ScaledLineHeight(typeScale);
        var layout = Layout(content, size, style.Weight, maxWidth);
        try
        {
            var ctLines = CTFrameGetLines(layout.Frame);
            var count = (int)CFArrayGetCount(ctLines);
            var shown = maxLines > 0 ? Math.Min(count, maxLines) : count;
            var lines = new List<MeasuredLine>(Math.Max(1, shown));
            var maxLineWidth = 0f;
            for (var i = 0; i < shown; i++)
            {
                var width = (float)CTLineGetTypographicBounds(
                    CFArrayGetValueAtIndex(ctLines, i), out _, out _, out _);
                if (content.Length == 0) width = 0;
                lines.Add(new MeasuredLine(width, Ellipsized: i == shown - 1 && shown < count));
                maxLineWidth = MathF.Max(maxLineWidth, width);
            }
            if (lines.Count == 0) lines.Add(new MeasuredLine(0, false));
            return new TextMeasurement(maxLineWidth, lines.Count * lineHeight, lineHeight, lines);
        }
        finally
        {
            ReleaseLayout(layout);
        }
    }

    public TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines, float scale)
    {
        if (content.Length == 0) return null;
        var size = style.ScaledSize(typeScale);
        var lineHeight = style.ScaledLineHeight(typeScale);
        var layout = Layout(content, size, style.Weight, maxWidth);
        try
        {
            var ctLines = CTFrameGetLines(layout.Frame);
            var count = (int)CFArrayGetCount(ctLines);
            var shown = maxLines > 0 ? Math.Min(count, maxLines) : count;
            if (shown == 0) return null;

            var widthDp = 0f;
            var metrics = new (IntPtr Line, float Ascent, float Descent)[shown];
            for (var i = 0; i < shown; i++)
            {
                var line = CFArrayGetValueAtIndex(ctLines, i);
                var w = (float)CTLineGetTypographicBounds(line, out var ascent, out var descent, out _);
                metrics[i] = (line, (float)ascent, (float)descent);
                widthDp = MathF.Max(widthDp, w);
            }

            var heightDp = shown * lineHeight;
            var pxWidth = Math.Max(1, (int)MathF.Ceiling(widthDp * scale));
            var pxHeight = Math.Max(1, (int)MathF.Ceiling(heightDp * scale));

            // Alpha-only bitmap; the CTM scales dp → device px, so positioning stays in dp.
            var context = CGBitmapContextCreate(IntPtr.Zero, (nuint)pxWidth, (nuint)pxHeight,
                8, (nuint)pxWidth, IntPtr.Zero, 7 /* kCGImageAlphaOnly */);
            if (context == IntPtr.Zero) return null;
            try
            {
                CGContextScaleCTM(context, scale, scale);
                for (var i = 0; i < shown; i++)
                {
                    // Center the glyph run on OUR line-height grid; CG is bottom-up.
                    var baselineFromTop = i * lineHeight
                        + (lineHeight - (metrics[i].Ascent + metrics[i].Descent)) / 2 + metrics[i].Ascent;
                    CGContextSetTextPosition(context, 0, heightDp - baselineFromTop);
                    CTLineDraw(metrics[i].Line, context);
                }

                var data = CGBitmapContextGetData(context);
                if (data == IntPtr.Zero) return null;
                var alpha = new byte[pxWidth * pxHeight];
                Marshal.Copy(data, alpha, 0, alpha.Length);
                return new TextRaster(pxWidth, pxHeight, alpha);
            }
            finally
            {
                CGContextRelease(context);
            }
        }
        finally
        {
            ReleaseLayout(layout);
        }
    }
}
