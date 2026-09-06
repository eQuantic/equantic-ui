namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// The Windows Imaging Component through its vtables — <c>wincodec.h</c> declaration order,
/// IUnknown first. WIC is the Windows twin of ImageIO: every format the OS decodes arrives through
/// it, and a bitmap of its making is what Direct2D draws into.
/// </summary>
internal static unsafe class Wic
{
    private static readonly Guid CLSID_WICImagingFactory = new("cacaf262-9370-4615-a13b-9f5539da4c0a");
    private static readonly Guid IID_IWICImagingFactory = new("ec5ec8a9-c395-4314-9c77-54d7a935ff70");

    public static readonly Guid PixelFormat32bppRGBA = new("f5c7ad2d-6a8d-43dd-a7a8-a29935261ae9");
    public static readonly Guid PixelFormat32bppPBGRA = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    public const uint DecodeMetadataCacheOnDemand = 0;
    public const uint BitmapCacheOnLoad = 2;
    public const uint LockRead = 1;
    public const uint DitherTypeNone = 0;
    public const uint PaletteTypeCustom = 0;

    public static void* CreateFactory()
    {
        Com.EnsureInitialized();
        return Com.Create(CLSID_WICImagingFactory, IID_IWICImagingFactory);
    }

    // ---- IWICImagingFactory -------------------------------------------------------------------

    public static int CreateDecoderFromStream(void* factory, void* stream, Guid* vendor, uint options, void** decoder) =>
        ((delegate* unmanaged<void*, void*, Guid*, uint, void**, int>)Com.Method(factory, 4))(factory, stream, vendor, options, decoder);

    public static int CreateFormatConverter(void* factory, void** converter) =>
        ((delegate* unmanaged<void*, void**, int>)Com.Method(factory, 10))(factory, converter);

    public static int CreateStream(void* factory, void** stream) =>
        ((delegate* unmanaged<void*, void**, int>)Com.Method(factory, 14))(factory, stream);

    public static int CreateBitmap(void* factory, uint width, uint height, Guid* pixelFormat, uint cacheOption, void** bitmap) =>
        ((delegate* unmanaged<void*, uint, uint, Guid*, uint, void**, int>)Com.Method(factory, 17))(factory, width, height, pixelFormat, cacheOption, bitmap);

    // ---- IWICStream ---------------------------------------------------------------------------

    public static int InitializeFromMemory(void* stream, byte* buffer, uint size) =>
        ((delegate* unmanaged<void*, byte*, uint, int>)Com.Method(stream, 16))(stream, buffer, size);

    // ---- IWICBitmapDecoder --------------------------------------------------------------------

    public static int GetFrame(void* decoder, uint index, void** frame) =>
        ((delegate* unmanaged<void*, uint, void**, int>)Com.Method(decoder, 13))(decoder, index, frame);

    // ---- IWICBitmapSource ---------------------------------------------------------------------

    public static int GetSize(void* source, uint* width, uint* height) =>
        ((delegate* unmanaged<void*, uint*, uint*, int>)Com.Method(source, 3))(source, width, height);

    public static int CopyPixels(void* source, void* rect, uint stride, uint bufferSize, byte* buffer) =>
        ((delegate* unmanaged<void*, void*, uint, uint, byte*, int>)Com.Method(source, 7))(source, rect, stride, bufferSize, buffer);

    // ---- IWICFormatConverter ------------------------------------------------------------------

    public static int Initialize(void* converter, void* source, Guid* destinationFormat, uint dither,
        void* palette, double alphaThresholdPercent, uint paletteType) =>
        ((delegate* unmanaged<void*, void*, Guid*, uint, void*, double, uint, int>)Com.Method(converter, 8))(
            converter, source, destinationFormat, dither, palette, alphaThresholdPercent, paletteType);

    // ---- IWICBitmap ---------------------------------------------------------------------------

    public static int Lock(void* bitmap, void* rect, uint flags, void** bitmapLock) =>
        ((delegate* unmanaged<void*, void*, uint, void**, int>)Com.Method(bitmap, 8))(bitmap, rect, flags, bitmapLock);

    // ---- IWICBitmapLock -----------------------------------------------------------------------

    public static int GetStride(void* bitmapLock, uint* stride) =>
        ((delegate* unmanaged<void*, uint*, int>)Com.Method(bitmapLock, 4))(bitmapLock, stride);

    public static int GetDataPointer(void* bitmapLock, uint* size, byte** data) =>
        ((delegate* unmanaged<void*, uint*, byte**, int>)Com.Method(bitmapLock, 5))(bitmapLock, size, data);
}
