namespace eQuantic.UI.Native.Framework;

/// <summary>A decoded image: straight sRGB RGBA bytes (4 per pixel).</summary>
public sealed record RgbaImage(int Width, int Height, byte[] Rgba);

/// <summary>
/// The platform image service (W4, the last Texture consumer): resolves an <c>Image.Source</c> to
/// decoded RGBA. Platform-owned, zero third-party — macOS decodes through ImageIO/CoreGraphics;
/// tests hand fakes in. Null (headless) keeps the SurfaceSubtle placeholder box. v1 sources are
/// local paths/bundled assets; URLs and async loading states are the fence.
/// </summary>
public interface IImageLoader
{
    RgbaImage? Load(string source);
}
