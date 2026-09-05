using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// Direct2D through its vtables — <c>d2d1.h</c> declaration order, IUnknown first. Methods that
/// RETURN a struct by value (GetSize, GetPixelFormat) are deliberately absent: the C++ ABI returns
/// them through a hidden pointer on some targets and in registers on others, and nothing here needs
/// them badly enough to find out which.
/// </summary>
internal static unsafe partial class D2D
{
    [LibraryImport("d2d1.dll")]
    private static partial int D2D1CreateFactory(uint factoryType, Guid* iid, void* options, void** factory);

    private static readonly Guid IID_ID2D1Factory = new("06152247-6f50-465a-9245-118bfd3b6007");

    public const uint FormatB8G8R8A8Unorm = 87;
    public const uint AlphaModePremultiplied = 1;
    public const uint TextAntialiasModeGrayscale = 2;
    public const uint FillModeWinding = 1;
    public const uint FigureBeginFilled = 0;
    public const uint FigureEndOpen = 0;
    public const uint FigureEndClosed = 1;
    public const uint CapStyleRound = 2;
    public const uint LineJoinRound = 2;
    public const uint DashStyleSolid = 0;
    public const uint DrawTextOptionsNone = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct ColorF
    {
        public float R, G, B, A;

        public ColorF(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        public static readonly ColorF Transparent = new(0, 0, 0, 0);
        public static readonly ColorF White = new(1, 1, 1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point2F
    {
        public float X, Y;

        public Point2F(float x, float y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix3x2
    {
        public float M11, M12, M21, M22, Dx, Dy;

        public static Matrix3x2 Scale(float sx, float sy) => new() { M11 = sx, M22 = sy };

        /// <summary>Translate by (tx, ty) in source units, THEN scale — a viewBox origin moved to
        /// zero before its units become pixels.</summary>
        public static Matrix3x2 TranslateThenScale(float tx, float ty, float sx, float sy) =>
            new() { M11 = sx, M22 = sy, Dx = tx * sx, Dy = ty * sy };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BezierSegment
    {
        public Point2F Point1, Point2, Point3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderTargetProperties
    {
        public uint Type;
        public uint Format;
        public uint AlphaMode;
        public float DpiX;
        public float DpiY;
        public uint Usage;
        public uint MinLevel;

        /// <summary>A premultiplied BGRA target at 96 dpi: the shell scales through the transform,
        /// so the DPI stays nominal and one dp is one unit.</summary>
        public static RenderTargetProperties PremultipliedBgra => new()
        {
            Format = FormatB8G8R8A8Unorm,
            AlphaMode = AlphaModePremultiplied,
            DpiX = 96,
            DpiY = 96,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StrokeStyleProperties
    {
        public uint StartCap;
        public uint EndCap;
        public uint DashCap;
        public uint LineJoin;
        public float MiterLimit;
        public uint DashStyle;
        public float DashOffset;
    }

    public static void* CreateFactory()
    {
        void* factory;
        var iid = IID_ID2D1Factory;
        Com.Check(D2D1CreateFactory(0 /* SINGLE_THREADED */, &iid, null, &factory), "Direct2D factory creation");
        return factory;
    }

    // ---- ID2D1Factory -------------------------------------------------------------------------

    public static int CreatePathGeometry(void* factory, void** geometry) =>
        ((delegate* unmanaged<void*, void**, int>)Com.Method(factory, 10))(factory, geometry);

    public static int CreateStrokeStyle(void* factory, StrokeStyleProperties* properties, float* dashes,
        uint dashCount, void** style) =>
        ((delegate* unmanaged<void*, StrokeStyleProperties*, float*, uint, void**, int>)Com.Method(factory, 11))(
            factory, properties, dashes, dashCount, style);

    public static int CreateWicBitmapRenderTarget(void* factory, void* wicBitmap, RenderTargetProperties* properties,
        void** target) =>
        ((delegate* unmanaged<void*, void*, RenderTargetProperties*, void**, int>)Com.Method(factory, 13))(
            factory, wicBitmap, properties, target);

    // ---- ID2D1RenderTarget --------------------------------------------------------------------

    public static int CreateSolidColorBrush(void* target, ColorF* color, void* brushProperties, void** brush) =>
        ((delegate* unmanaged<void*, ColorF*, void*, void**, int>)Com.Method(target, 8))(target, color, brushProperties, brush);

    public static void DrawGeometry(void* target, void* geometry, void* brush, float strokeWidth, void* strokeStyle) =>
        ((delegate* unmanaged<void*, void*, void*, float, void*, void>)Com.Method(target, 22))(target, geometry, brush, strokeWidth, strokeStyle);

    public static void FillGeometry(void* target, void* geometry, void* brush, void* opacityBrush) =>
        ((delegate* unmanaged<void*, void*, void*, void*, void>)Com.Method(target, 23))(target, geometry, brush, opacityBrush);

    public static void DrawTextLayout(void* target, Point2F origin, void* layout, void* brush, uint options) =>
        ((delegate* unmanaged<void*, Point2F, void*, void*, uint, void>)Com.Method(target, 28))(target, origin, layout, brush, options);

    public static void SetTransform(void* target, Matrix3x2* transform) =>
        ((delegate* unmanaged<void*, Matrix3x2*, void>)Com.Method(target, 30))(target, transform);

    public static void SetTextAntialiasMode(void* target, uint mode) =>
        ((delegate* unmanaged<void*, uint, void>)Com.Method(target, 34))(target, mode);

    public static void SetTextRenderingParams(void* target, void* renderingParams) =>
        ((delegate* unmanaged<void*, void*, void>)Com.Method(target, 36))(target, renderingParams);

    public static void Clear(void* target, ColorF* color) =>
        ((delegate* unmanaged<void*, ColorF*, void>)Com.Method(target, 47))(target, color);

    public static void BeginDraw(void* target) =>
        ((delegate* unmanaged<void*, void>)Com.Method(target, 48))(target);

    public static int EndDraw(void* target) =>
        ((delegate* unmanaged<void*, ulong*, ulong*, int>)Com.Method(target, 49))(target, null, null);

    // ---- ID2D1PathGeometry --------------------------------------------------------------------

    public static int Open(void* geometry, void** sink) =>
        ((delegate* unmanaged<void*, void**, int>)Com.Method(geometry, 17))(geometry, sink);

    // ---- ID2D1GeometrySink --------------------------------------------------------------------

    public static void SetFillMode(void* sink, uint fillMode) =>
        ((delegate* unmanaged<void*, uint, void>)Com.Method(sink, 3))(sink, fillMode);

    public static void BeginFigure(void* sink, Point2F start, uint figureBegin) =>
        ((delegate* unmanaged<void*, Point2F, uint, void>)Com.Method(sink, 5))(sink, start, figureBegin);

    public static void EndFigure(void* sink, uint figureEnd) =>
        ((delegate* unmanaged<void*, uint, void>)Com.Method(sink, 8))(sink, figureEnd);

    public static int Close(void* sink) =>
        ((delegate* unmanaged<void*, int>)Com.Method(sink, 9))(sink);

    public static void AddLine(void* sink, Point2F point) =>
        ((delegate* unmanaged<void*, Point2F, void>)Com.Method(sink, 10))(sink, point);

    public static void AddBezier(void* sink, BezierSegment* bezier) =>
        ((delegate* unmanaged<void*, BezierSegment*, void>)Com.Method(sink, 11))(sink, bezier);
}
