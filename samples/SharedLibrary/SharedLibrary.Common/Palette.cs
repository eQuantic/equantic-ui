using eQuantic.UI.Primitives;

namespace SharedLibrary.Common;

/// <summary>The brand's fixed inks, shared by every app of the solution: a static class whose
/// members are read by name, the first of the shapes a shared library holds.</summary>
public static class Palette
{
    public static readonly ColorToken Cobalt = Ink(0x2347A8);
    public static readonly ColorToken Terracotta = Ink(0xB4491F);

    /// <summary>The inks' names, in the order a page lists them.</summary>
    public static readonly string[] Names = ["Cobalt", "Terracotta"];

    /// <summary>A hex literal as an opaque token.</summary>
    public static ColorToken Ink(uint rgb) =>
        new(Color.FromRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF));
}
