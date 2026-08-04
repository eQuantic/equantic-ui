using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Android's picture picker.
/// <para>
/// The PHOTO PICKER where the device has one (13 and up, and back to 11 through the system
/// component), because it runs outside the app: the user picks, the app receives exactly that, and
/// no permission is involved. Below that, ACTION_OPEN_DOCUMENT — still a system UI, still scoped to
/// what was chosen. Neither path asks for READ_MEDIA_IMAGES, and that is deliberate: a permission
/// prompt for access the app does not need is how apps teach users to say no.
/// </para>
/// </summary>
internal sealed class AndroidPhotoLibrary : IPhotoLibrary
{
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
        if (PhotonActivity.Current is not { } activity) return Array.Empty<ImageData>();

        var multiple = limit != 1;
        var intent = PickerIntent(multiple, limit);
        var result = await activity.PickAsync(intent, cancellationToken);
        if (result is null) return Array.Empty<ImageData>();

        var uris = UrisOf(result, multiple, limit);
        var picked = new List<ImageData>(uris.Count);
        foreach (var uri in uris)
        {
            if (Read(activity, uri) is { } image) picked.Add(image);
        }
        return picked;
    }

    /// <summary>The best picker this device has.</summary>
    private static Intent PickerIntent(bool multiple, int limit)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var picker = new Intent(MediaStore.ActionPickImages);
            picker.SetType("image/*");
            if (multiple && limit > 1) picker.PutExtra(MediaStore.ExtraPickImagesMax, limit);
            return picker;
        }

        var documents = new Intent(Intent.ActionOpenDocument);
        documents.AddCategory(Intent.CategoryOpenable);
        documents.SetType("image/*");
        if (multiple) documents.PutExtra(Intent.ExtraAllowMultiple, true);
        return documents;
    }

    /// <summary>One item comes back in the data; several come back in a clip. Both shapes are
    /// normal, and an app that reads only one of them silently drops the other case.</summary>
    private static IReadOnlyList<global::Android.Net.Uri> UrisOf(Intent result, bool multiple, int limit)
    {
        var uris = new List<global::Android.Net.Uri>();
        if (result.ClipData is { } clip)
        {
            var count = limit > 0 ? Math.Min(clip.ItemCount, limit) : clip.ItemCount;
            for (var i = 0; i < count; i++)
            {
                if (clip.GetItemAt(i)?.Uri is { } uri) uris.Add(uri);
            }
        }
        else if (result.Data is { } single)
        {
            uris.Add(single);
        }
        return uris;
    }

    private static ImageData? Read(Activity activity, global::Android.Net.Uri uri)
    {
        try
        {
            using var stream = activity.ContentResolver?.OpenInputStream(uri);
            if (stream is null) return null;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();

            var (width, height) = ImageHeader.Measure(bytes);
            var mime = activity.ContentResolver?.GetType(uri) ?? "image/jpeg";
            return new ImageData(bytes, mime, width, height, NameOf(activity, uri));
        }
        catch (Exception)
        {
            // A URI the user revoked between picking and reading, a provider that died: the app
            // asked for a picture and there is none. That is the same answer as cancelling.
            return null;
        }
    }

    /// <summary>The display name the provider carries, when it carries one.</summary>
    private static string? NameOf(Activity activity, global::Android.Net.Uri uri)
    {
        using var cursor = activity.ContentResolver?.Query(uri, null, null, null, null);
        if (cursor?.MoveToFirst() != true) return null;
        var column = cursor.GetColumnIndex(OpenableColumns.DisplayName);
        return column >= 0 ? cursor.GetString(column) : null;
    }
}
