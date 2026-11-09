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
    /// The launcher's sizes. 512 and 192 are the manifest's install icons, and 180 is what iOS Safari
    /// pins to a home screen.
    /// </summary>
    private static readonly (string Name, int Size)[] Sizes =
    [
        ("icon-512.png", 512),
        ("icon-192.png", 192),
        ("apple-touch-icon.png", 180),
    ];

    /// <summary>
    /// The tab's, in one <c>favicon.ico</c>: 16 for a tab, 32 for a tab on a dense screen and for the
    /// bookmarks, 48 for a desktop shortcut. An ICO rather than a PNG because every browser and every
    /// crawler asks for <c>/favicon.ico</c> at the site's root whatever the page links, and the server
    /// answers it from this file (#1240).
    /// </summary>
    private static readonly (int Size, bool Png)[] FaviconSizes = [(16, false), (32, false), (48, false)];

    /// <summary>
    /// Writes the set: the launcher's sizes and the manifest from the icon, and <c>favicon.ico</c> from
    /// its small cut (<see cref="eQuantic.UI.Primitives.AppIconUse.Small"/>) when there is one, as a
    /// drawn icon has, and from the icon otherwise, as a PNG artwork is.
    /// </summary>
    public static void Write(string outDir, string appName, int sourceSize, byte[] sourceRgba,
        (int Size, byte[] Rgba)? small = null)
    {
        Directory.CreateDirectory(outDir);

        foreach (var (name, size) in Sizes)
        {
            var pixels = size == sourceSize ? sourceRgba : Downscale.Box(sourceRgba, sourceSize, size);
            File.WriteAllBytes(Path.Combine(outDir, name), PngCodec.Encode(size, size, pixels));
        }

        var (faviconSize, faviconRgba) = small ?? (sourceSize, sourceRgba);
        File.WriteAllBytes(Path.Combine(outDir, "favicon.ico"), IconFile.Encode(faviconSize, faviconRgba, FaviconSizes));

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
