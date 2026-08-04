using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The Mac's answer to "let the user choose a picture": an open panel.
/// <para>
/// A desktop has no photo library to ask permission FOR — the file system is the library, and
/// choosing a file IS the grant. That is why the permission here is always granted: the app can see
/// exactly what the user picked and nothing else, which is the same guarantee the modern phone
/// pickers give. Reporting NotDetermined and prompting would be theatre.
/// </para>
/// </summary>
internal sealed class MacOSPhotoLibrary : IPhotoLibrary
{
    private static readonly string[] ImageTypes =
        ["public.jpeg", "public.png", "public.heic", "com.compuserve.gif", "public.tiff"];

    public bool IsAvailable => true;

    public ValueTask<PermissionState> GetPermissionAsync(CancellationToken cancellationToken = default) =>
        new(PermissionState.Granted);

    public async ValueTask<ImageData?> PickImageAsync(CancellationToken cancellationToken = default)
    {
        var picked = await PickImagesAsync(1, cancellationToken);
        return picked.Count > 0 ? picked[0] : null;
    }

    public ValueTask<IReadOnlyList<ImageData>> PickImagesAsync(int limit = 0,
        CancellationToken cancellationToken = default)
    {
        AppKit.LoadFrameworks();
        var panel = Send(objc_getClass("NSOpenPanel"), Sel("openPanel"));
        SendVoid(panel, Sel("setCanChooseFiles:"), true);
        SendVoid(panel, Sel("setCanChooseDirectories:"), false);
        SendVoid(panel, Sel("setAllowsMultipleSelection:"), limit != 1);
        SendVoid(panel, Sel("setAllowedFileTypes:"), StringArray(ImageTypes));

        // runModal blocks on the caller's thread, which is the UI thread — and that is correct for
        // a modal panel: the app is not meant to be doing anything else while it is up.
        if (SendLong(panel, Sel("runModal")) != 1 /* NSModalResponseOK */)
            return new ValueTask<IReadOnlyList<ImageData>>(Array.Empty<ImageData>());

        var urls = Send(panel, Sel("URLs"));
        var count = (int)SendULong(urls, Sel("count"));
        if (limit > 0) count = Math.Min(count, limit);

        var picked = new List<ImageData>(count);
        for (var i = 0; i < count; i++)
        {
            var url = Send(urls, Sel("objectAtIndex:"), (nuint)i);
            var path = FromNSString(Send(url, Sel("path")));
            if (path is null || !File.Exists(path)) continue;
            picked.Add(Read(path));
        }

        return new ValueTask<IReadOnlyList<ImageData>>(picked);
    }

    /// <summary>The file, as it is on disk. Dimensions come from the header — cheap, and the caller
    /// usually wants to know before deciding what to do with it.</summary>
    private static ImageData Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (width, height) = ImageHeader.Measure(bytes);
        return new ImageData(bytes, MimeOf(path), width, height, Path.GetFileName(path));
    }

    private static string MimeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".heic" or ".heif" => "image/heic",
        ".tif" or ".tiff" => "image/tiff",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    private static IntPtr Sel(string name) => sel_registerName(name);
}
