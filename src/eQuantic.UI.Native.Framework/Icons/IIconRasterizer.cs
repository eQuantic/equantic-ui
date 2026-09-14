namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The platform icon rasterizer (W4): turns an <see cref="Primitives.IconGlyph"/> into an A8
/// coverage raster at device scale — the Texture command draws it tinted, exactly like text.
/// Null (tests, headless) keeps the deterministic disc placeholder.
/// <para>
/// The box has TWO extents because a glyph is not always square: an icon asks for the same number
/// twice, a figure asks for its viewBox's aspect. Each axis scales independently (an
/// <c>&lt;svg&gt;</c> without <c>preserveAspectRatio</c>), so the caller decides whether the shape
/// is fitted or stretched — the rasterizer never guesses on its behalf.
/// </para>
/// </summary>
public interface IIconRasterizer
{
    TextRaster? Rasterize(Primitives.IconGlyph glyph, float widthDp, float heightDp, float scale);
}
