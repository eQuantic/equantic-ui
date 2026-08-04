using System.Buffers.Binary;

using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Build;

/// <summary>
/// The macOS icon file. An <c>.icns</c> is a container the Finder, the Dock and the app switcher
/// all read: a 'icns' magic, a big-endian length, then one chunk per representation — a four-byte
/// type naming the size, a length, and a PNG.
/// <para>
/// Writing it is what makes a desktop head an APPLICATION rather than a process with a window: no
/// icon file, no icon anywhere the user looks. The set below is the one Apple's own tooling emits
/// (16…512 at 1x and 2x), derived from the same <c>Assets/AppIcon</c> that feeds iOS and Android —
/// one source of truth, three platforms.
/// </para>
/// </summary>
public static class MacIcons
{
    /// <summary>The representations, and the OSType each one is filed under.</summary>
    private static readonly (string Type, int Size)[] Representations =
    [
        ("icp4", 16), ("icp5", 32), ("icp6", 64),
        ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
        ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512),
    ];

    public static void Write(string path, int sourceSize, byte[] sourceRgba)
    {
        var chunks = new List<byte[]>();
        var cache = new Dictionary<int, byte[]>();

        foreach (var (type, size) in Representations)
        {
            if (!cache.TryGetValue(size, out var png))
            {
                var pixels = size == sourceSize ? sourceRgba : Downscale.Box(sourceRgba, sourceSize, size);
                png = PngCodec.Encode(size, size, pixels);
                cache[size] = png;
            }

            var chunk = new byte[8 + png.Length];
            for (var i = 0; i < 4; i++) chunk[i] = (byte)type[i];
            BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(4, 4), chunk.Length);
            png.CopyTo(chunk, 8);
            chunks.Add(chunk);
        }

        var total = 8 + chunks.Sum(chunk => chunk.Length);
        using var file = File.Create(path);
        Span<byte> header = stackalloc byte[8];
        "icns"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], total);
        file.Write(header);
        foreach (var chunk in chunks) file.Write(chunk);
    }
}
