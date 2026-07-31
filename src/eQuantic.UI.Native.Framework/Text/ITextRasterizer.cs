using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// A rasterized text BLOCK (W4): pure A8 coverage at device scale — color is the draw command's
/// tint, so one raster serves every theme mode. Width/Height are PIXELS (dp × scale).
/// </summary>
public sealed record TextRaster(int Width, int Height, byte[] Alpha);

/// <summary>
/// The platform text service (W4): turns a measured text block into an A8 coverage raster the
/// engine's Texture command draws. Platform-owned and ZERO third-party by design — macOS uses
/// CoreText (a system framework, like AppKit/Metal); other platforms plug their own. The SAME
/// engine must also serve <see cref="ITextMeasurer"/>, so layout line breaks and raster line
/// breaks agree by construction. Null (tests, headless) keeps the deterministic placeholder bars.
/// </summary>
public interface ITextRasterizer
{
    /// <summary>
    /// Rasterizes <paramref name="content"/> in <paramref name="style"/> (Dynamic-Type scaled by
    /// <paramref name="typeScale"/>), wrapped at <paramref name="maxWidth"/> dp and truncated to
    /// <paramref name="maxLines"/> (0 = unlimited), at <paramref name="scale"/> device pixels per
    /// dp. Lines sit on the STYLE's line-height grid (the layout's contract), not the font's.
    /// Null = the platform cannot rasterize this block (the caller falls back to bars).
    /// </summary>
    TextRaster? Rasterize(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines, float scale);
}
