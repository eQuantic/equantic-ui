namespace eQuantic.UI.Build;

/// <summary>
/// The Windows icon file. An <c>.ico</c> is a directory of images the shell picks from by size —
/// the taskbar wants 32 or 48, Explorer's tiles want 256, the title bar 16 — and it is what the
/// executable carries as its icon resource, which is how a Photon app gets a face in the Start menu
/// and on the taskbar without anybody drawing eight PNGs by hand.
/// <para>
/// The container is <see cref="IconFile"/>'s, shared with the web's <c>favicon.ico</c>. Derived from
/// the same <c>Assets/AppIcon</c> that feeds the phones and the Mac — one source of truth, four
/// platforms.
/// </para>
/// </summary>
public static class WindowsIcons
{
    /// <summary>The sizes Windows asks for, and whether each travels as a DIB or a PNG.</summary>
    private static readonly (int Size, bool Png)[] Representations =
    [
        (16, false), (20, false), (24, false), (32, false), (40, false), (48, false), (64, false), (256, true),
    ];

    public static void Write(string path, int sourceSize, byte[] sourceRgba)
    {
        var file = IconFile.Encode(sourceSize, sourceRgba, Representations);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, file);
    }
}
