using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Windows.Graphics;

/// <summary>
/// DirectWrite through its vtables — the slots in declaration order from <c>dwrite.h</c>, IUnknown
/// first. Only what the text service calls is here; a slot is added when a caller appears, never
/// ahead of one.
/// </summary>
internal static unsafe partial class DWrite
{
    [LibraryImport("dwrite.dll")]
    private static partial int DWriteCreateFactory(uint factoryType, Guid* iid, void** factory);

    private static readonly Guid IID_IDWriteFactory = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");

    public const uint StyleNormal = 0;
    public const uint StyleItalic = 2;
    public const uint StretchNormal = 5;
    public const uint WordWrappingWrap = 0;
    public const uint LineSpacingUniform = 1;
    public const uint PixelGeometryFlat = 0;
    /// <summary>DWRITE_RENDERING_MODE_NATURAL_SYMMETRIC — unhinted, symmetric anti-aliasing in both
    /// axes: the closest DirectWrite comes to the coverage CoreText produces, so a glyph measures
    /// and looks the same as the Mac's within a pixel.</summary>
    public const uint RenderingModeNaturalSymmetric = 5;

    [StructLayout(LayoutKind.Sequential)]
    public struct FontMetrics
    {
        public ushort DesignUnitsPerEm;
        public ushort Ascent;
        public ushort Descent;
        public short LineGap;
        public ushort CapHeight;
        public ushort XHeight;
        public short UnderlinePosition;
        public ushort UnderlineThickness;
        public short StrikethroughPosition;
        public ushort StrikethroughThickness;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TextMetrics
    {
        public float Left;
        public float Top;
        public float Width;
        public float WidthIncludingTrailingWhitespace;
        public float Height;
        public float LayoutWidth;
        public float LayoutHeight;
        public uint MaxBidiReorderingDepth;
        public uint LineCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LineMetrics
    {
        public uint Length;
        public uint TrailingWhitespaceLength;
        public uint NewlineLength;
        public float Height;
        public float Baseline;
        public int IsTrimmed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterMetrics
    {
        public float Width;
        public ushort Length;
        /// <summary>Bit 0 canWrapLineAfter, bit 1 isWhitespace, bit 2 isNewline, bit 3 isSoftHyphen,
        /// bit 4 isRightToLeft — a C bitfield, read by mask.</summary>
        public ushort Flags;

        public bool IsWhitespace => (Flags & 0x2) != 0;
        public bool IsNewline => (Flags & 0x4) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OverhangMetrics
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }

    public static void* CreateFactory()
    {
        void* factory;
        var iid = IID_IDWriteFactory;
        Com.Check(DWriteCreateFactory(0 /* SHARED */, &iid, &factory), "DirectWrite factory creation");
        return factory;
    }

    // ---- IDWriteFactory -----------------------------------------------------------------------

    public static int GetSystemFontCollection(void* factory, void** collection) =>
        ((delegate* unmanaged<void*, void**, int, int>)Com.Method(factory, 3))(factory, collection, 0);

    public static int CreateCustomRenderingParams(void* factory, float gamma, float enhancedContrast,
        float clearTypeLevel, uint pixelGeometry, uint renderingMode, void** renderingParams) =>
        ((delegate* unmanaged<void*, float, float, float, uint, uint, void**, int>)Com.Method(factory, 12))(
            factory, gamma, enhancedContrast, clearTypeLevel, pixelGeometry, renderingMode, renderingParams);

    public static int CreateTextFormat(void* factory, char* familyName, void* collection, int weight,
        uint style, uint stretch, float size, char* locale, void** format) =>
        ((delegate* unmanaged<void*, char*, void*, int, uint, uint, float, char*, void**, int>)Com.Method(factory, 15))(
            factory, familyName, collection, weight, style, stretch, size, locale, format);

    public static int CreateTextLayout(void* factory, char* text, uint length, void* format,
        float maxWidth, float maxHeight, void** layout) =>
        ((delegate* unmanaged<void*, char*, uint, void*, float, float, void**, int>)Com.Method(factory, 18))(
            factory, text, length, format, maxWidth, maxHeight, layout);

    // ---- IDWriteFontCollection ----------------------------------------------------------------

    public static int GetFontFamily(void* collection, uint index, void** family) =>
        ((delegate* unmanaged<void*, uint, void**, int>)Com.Method(collection, 4))(collection, index, family);

    public static int FindFamilyName(void* collection, char* familyName, uint* index, int* exists) =>
        ((delegate* unmanaged<void*, char*, uint*, int*, int>)Com.Method(collection, 5))(collection, familyName, index, exists);

    // ---- IDWriteFontFamily --------------------------------------------------------------------

    public static int GetFirstMatchingFont(void* family, int weight, uint stretch, uint style, void** font) =>
        ((delegate* unmanaged<void*, int, uint, uint, void**, int>)Com.Method(family, 7))(family, weight, stretch, style, font);

    // ---- IDWriteFont --------------------------------------------------------------------------

    public static void GetMetrics(void* font, FontMetrics* metrics) =>
        ((delegate* unmanaged<void*, FontMetrics*, void>)Com.Method(font, 11))(font, metrics);

    // ---- IDWriteTextFormat --------------------------------------------------------------------

    public static int SetWordWrapping(void* format, uint wrapping) =>
        ((delegate* unmanaged<void*, uint, int>)Com.Method(format, 5))(format, wrapping);

    public static int SetLineSpacing(void* format, uint method, float lineSpacing, float baseline) =>
        ((delegate* unmanaged<void*, uint, float, float, int>)Com.Method(format, 10))(format, method, lineSpacing, baseline);

    // ---- IDWriteTextLayout --------------------------------------------------------------------

    public static int SetMaxHeight(void* layout, float maxHeight) =>
        ((delegate* unmanaged<void*, float, int>)Com.Method(layout, 29))(layout, maxHeight);

    public static int GetLineMetrics(void* layout, LineMetrics* lines, uint maxLineCount, uint* actualLineCount) =>
        ((delegate* unmanaged<void*, LineMetrics*, uint, uint*, int>)Com.Method(layout, 59))(layout, lines, maxLineCount, actualLineCount);

    public static int GetMetrics(void* layout, TextMetrics* metrics) =>
        ((delegate* unmanaged<void*, TextMetrics*, int>)Com.Method(layout, 60))(layout, metrics);

    public static int GetOverhangMetrics(void* layout, OverhangMetrics* overhangs) =>
        ((delegate* unmanaged<void*, OverhangMetrics*, int>)Com.Method(layout, 61))(layout, overhangs);

    public static int GetClusterMetrics(void* layout, ClusterMetrics* clusters, uint maxClusterCount, uint* actualClusterCount) =>
        ((delegate* unmanaged<void*, ClusterMetrics*, uint, uint*, int>)Com.Method(layout, 62))(layout, clusters, maxClusterCount, actualClusterCount);
}
