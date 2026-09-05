using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The desktop's answer to "let the user choose a picture": the open dialog, filtered to images.
/// <para>
/// A desktop has no photo library to ask permission FOR — the file system is the library, and
/// choosing a file IS the grant. That is why the permission here is always granted: the app can see
/// exactly what the user picked and nothing else, which is the same guarantee the modern phone
/// pickers give. Reporting NotDetermined and prompting would be theatre.
/// </para>
/// </summary>
internal sealed class WindowsPhotoLibrary(IFileDialogs dialogs) : IPhotoLibrary
{
    private static readonly FileFilter[] ImageFilters =
    [
        new("Images", "png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "webp", "heic"),
    ];

    public bool IsAvailable => true;

    public ValueTask<PermissionState> GetPermissionAsync(CancellationToken cancellationToken = default) =>
        new(PermissionState.Granted);

    public async ValueTask<ImageData?> PickImageAsync(CancellationToken cancellationToken = default)
    {
        var picked = await PickImagesAsync(1, cancellationToken);
        return picked.Count > 0 ? picked[0] : null;
    }

    public async ValueTask<IReadOnlyList<ImageData>> PickImagesAsync(int limit = 0,
        CancellationToken cancellationToken = default)
    {
        var paths = limit == 1
            ? await dialogs.PickFile(null, ImageFilters) is { } one ? [one] : []
            : await dialogs.PickFiles(null, ImageFilters);
        var count = limit > 0 ? Math.Min(paths.Count, limit) : paths.Count;
        var picked = new List<ImageData>(count);
        for (var i = 0; i < count; i++)
        {
            if (!File.Exists(paths[i])) continue;
            picked.Add(Read(paths[i]));
        }
        return picked;
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
        ".bmp" => "image/bmp",
        ".heic" or ".heif" => "image/heic",
        ".tif" or ".tiff" => "image/tiff",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
