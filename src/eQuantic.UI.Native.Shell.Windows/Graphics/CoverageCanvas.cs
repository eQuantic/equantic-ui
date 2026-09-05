using eQuantic.UI.Native.Framework;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// A Direct2D render target over a WIC bitmap, read back as A8 COVERAGE — the one shape both the
/// text service and the icon rasterizer need: draw white onto transparent, take the alpha channel.
/// Premultiplied BGRA underneath, so a white brush leaves alpha equal to coverage exactly, with no
/// un-premultiply to round through.
/// </summary>
internal static unsafe class CoverageCanvas
{
    /// <summary>
    /// Draws through <paramref name="draw"/> — which receives the render target and a white brush —
    /// and returns the alpha plane as a <see cref="TextRaster"/>. Null when the system declined a
    /// bitmap or a target (out of memory, a zero device), which the caller answers with its own
    /// fallback rather than an exception mid-frame.
    /// </summary>
    public static TextRaster? Draw(void* d2d, void* wic, int width, int height, int padTop,
        Action<IntPtr, IntPtr> draw)
    {
        void* bitmap = null;
        void* target = null;
        void* brush = null;
        void* bitmapLock = null;
        try
        {
            var format = Wic.PixelFormat32bppPBGRA;
            if (Wic.CreateBitmap(wic, (uint)width, (uint)height, &format, Wic.BitmapCacheOnLoad, &bitmap) < 0)
                return null;

            var properties = D2D.RenderTargetProperties.PremultipliedBgra;
            if (D2D.CreateWicBitmapRenderTarget(d2d, bitmap, &properties, &target) < 0) return null;

            var white = D2D.ColorF.White;
            Com.Check(D2D.CreateSolidColorBrush(target, &white, null, &brush), "brush creation");

            D2D.BeginDraw(target);
            var transparent = D2D.ColorF.Transparent;
            D2D.Clear(target, &transparent);
            draw((IntPtr)target, (IntPtr)brush);
            Com.Check(D2D.EndDraw(target), "Direct2D EndDraw");

            Com.Check(Wic.Lock(bitmap, null, Wic.LockRead, &bitmapLock), "bitmap lock");
            uint stride;
            Com.Check(Wic.GetStride(bitmapLock, &stride), "bitmap stride");
            uint size;
            byte* data;
            Com.Check(Wic.GetDataPointer(bitmapLock, &size, &data), "bitmap data");

            var alpha = new byte[width * height];
            for (var y = 0; y < height; y++)
            {
                var row = data + y * stride;
                var destination = y * width;
                for (var x = 0; x < width; x++) alpha[destination + x] = row[x * 4 + 3];
            }
            return new TextRaster(width, height, alpha, padTop);
        }
        finally
        {
            Com.Release(ref bitmapLock);
            Com.Release(ref brush);
            Com.Release(ref target);
            Com.Release(ref bitmap);
        }
    }
}
