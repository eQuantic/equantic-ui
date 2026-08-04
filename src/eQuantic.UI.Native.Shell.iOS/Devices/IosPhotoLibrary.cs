using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using Foundation;
using PhotosUI;
using UIKit;
using UniformTypeIdentifiers;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The iPhone's picture picker.
/// <para>
/// PHPicker, not the old image picker, and the difference is the whole point: PHPicker runs OUT OF
/// PROCESS. The app never sees the library — it receives exactly the items the user tapped — which
/// is why it needs no permission at all. Asking for one would be worse than pointless: it would put
/// a prompt in front of the user for access the app does not want and cannot use.
/// </para>
/// <para>
/// So <see cref="GetPermissionAsync"/> answers Granted. That is not a shortcut around the
/// permission model; it is the honest answer for a picker whose contract is "you get what was
/// chosen". The declaration on the builder still writes the manifest key, because an app that
/// later reaches for the library ITSELF will need it.
/// </para>
/// </summary>
internal sealed class IosPhotoLibrary : IPhotoLibrary
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
        var presenter = Presenter();
        if (presenter is null) return Array.Empty<ImageData>();

        var configuration = new PHPickerConfiguration
        {
            // Zero means "as many as you like" to PHPicker too, so the app's meaning passes through.
            SelectionLimit = limit,
            Filter = PHPickerFilter.ImagesFilter,
        };

        var picker = new PHPickerViewController(configuration);
        var results = new TaskCompletionSource<PHPickerResult[]>();
        picker.Delegate = new PickerDelegate(results);

        await presenter.PresentViewControllerAsync(picker, animated: true);
        var chosen = await results.Task.WaitAsync(cancellationToken);

        var picked = new List<ImageData>(chosen.Length);
        foreach (var result in chosen)
        {
            if (await LoadAsync(result) is { } image) picked.Add(image);
        }
        return picked;
    }

    /// <summary>
    /// The item's own DATA, not a decoded UIImage: a picker hands back what the file is — a HEIC
    /// stays a HEIC — and re-encoding it here would lose quality to answer a question nobody asked.
    /// </summary>
    private static async Task<ImageData?> LoadAsync(PHPickerResult result)
    {
        var provider = result.ItemProvider;
        // The FIRST identifier that really is an image. A provider offers several representations
        // and the order is the system's preference, so taking the first that conforms is taking the
        // best one on offer.
        var type = provider.RegisteredTypeIdentifiers.FirstOrDefault(identifier =>
            UTType.CreateFromIdentifier(identifier)?.ConformsTo(UTTypes.Image) == true);
        if (type is null) return null;

        var completion = new TaskCompletionSource<NSData?>();
        provider.LoadDataRepresentation(type, (data, _) => completion.TrySetResult(data));
        var loaded = await completion.Task;
        if (loaded is null) return null;

        var bytes = loaded.ToArray();
        var (width, height) = ImageHeader.Measure(bytes);
        return new ImageData(bytes, MimeOf(type), width, height, provider.SuggestedName);
    }

    private static string MimeOf(string typeIdentifier) => typeIdentifier switch
    {
        "public.png" => "image/png",
        "public.heic" or "public.heif" => "image/heic",
        "com.compuserve.gif" => "image/gif",
        "org.webmproject.webp" => "image/webp",
        _ => "image/jpeg",
    };

    /// <summary>Whatever is on screen right now, including anything already presented over it —
    /// presenting onto a controller that is itself covered does nothing at all.</summary>
    private static UIViewController? Presenter()
    {
        var controller = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .SelectMany(scene => scene.Windows)
            .FirstOrDefault(window => window.IsKeyWindow)?.RootViewController;

        while (controller?.PresentedViewController is { } presented) controller = presented;
        return controller;
    }

    private sealed class PickerDelegate(TaskCompletionSource<PHPickerResult[]> results)
        : PHPickerViewControllerDelegate
    {
        public override void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] result)
        {
            // Dismissing is the CALLER's job on this delegate — the picker stays up otherwise, and
            // an empty result (the user tapped Cancel) has to dismiss just the same.
            picker.DismissViewController(animated: true, completionHandler: null);
            results.TrySetResult(result);
        }
    }
}
