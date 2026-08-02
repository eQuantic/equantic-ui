using eQuantic.UI.Codegen;
using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Build;

/// <summary>
/// The same icon, for a browser. A phone installs one big square and derives the rest; the web asks
/// for the sizes up front and for a manifest describing them, so that is what gets written — from
/// the very same source, so an app's icon is stated ONCE and appears everywhere it belongs.
/// </summary>
public static class WebIcons
{
    /// <summary>
    /// What a page actually needs today. 512 and 192 are the manifest's install icons, 180 is what
    /// iOS Safari pins to a home screen, and 32 is the tab. No .ico: every browser that matters has
    /// taken a PNG favicon for a decade, and the ones that do not are not getting this app anyway.
    /// </summary>
    private static readonly (string Name, int Size)[] Sizes =
    [
        ("icon-512.png", 512),
        ("icon-192.png", 192),
        ("apple-touch-icon.png", 180),
        ("favicon-32.png", 32),
    ];

    public static void Write(string outDir, string appName, int sourceSize, byte[] sourceRgba)
    {
        Directory.CreateDirectory(outDir);

        foreach (var (name, size) in Sizes)
        {
            var pixels = size == sourceSize ? sourceRgba : Downscale.Box(sourceRgba, sourceSize, size);
            File.WriteAllBytes(Path.Combine(outDir, name), PngCodec.Encode(size, size, pixels));
        }

        // A manifest is what turns a page into something a phone will install. Written through the
        // JSON writer for the same reason the asset catalog is: a generated file people may read.
        File.WriteAllText(Path.Combine(outDir, "site.webmanifest"), JsonWriter.Document(manifest => manifest
            .String("name", appName)
            .String("short_name", appName)
            .Array("icons", icons =>
            {
                icons.Element(icon => icon
                    .String("src", "/_equantic/icons/icon-192.png")
                    .String("sizes", "192x192")
                    .String("type", "image/png"));
                icons.Element(icon => icon
                    .String("src", "/_equantic/icons/icon-512.png")
                    .String("sizes", "512x512")
                    .String("type", "image/png"));
            })
            .String("display", "standalone")));
    }

}
