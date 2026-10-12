using Android.Content;
using Android.Hardware.Camera2;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The camera, through Camera2 — the framework's own API, bound by .NET for Android, so a preview
/// costs an app no dependency and the SDK no connector. CameraX would be friendlier to write against
/// and is a Jetpack library on top of this same API: a preview and a still need nothing it adds.
/// <para>
/// FRAMES AS PIXELS, as on the Apple shells and for the same reason: a platform preview surface
/// would composite outside the engine, above every clip, corner radius and overlay the display list
/// draws. An <see cref="global::Android.Media.ImageReader"/> hands each frame over instead, and the
/// preview is an ordinary texture (see <see cref="AndroidCameraSession"/>).
/// </para>
/// </summary>
internal sealed class AndroidCamera : ICamera
{
    private const string CameraPermission = global::Android.Manifest.Permission.Camera;
    private const string Preferences = "eq.capabilities";
    private const string AskedKey = "camera.asked";

    private static CameraManager? Manager =>
        PhotonActivity.Current?.GetSystemService(Context.CameraService) as CameraManager;

    public bool IsAvailable => Manager is { } manager && Choose(manager) is not null;

    /// <summary>
    /// Three answers, though Android itself gives two: it says "granted" or "not", and cannot tell
    /// "never asked" from "refused". The shell remembers that it asked, so a refusal reads as one
    /// and a page can point at Settings instead of asking again.
    /// </summary>
    public PermissionState Permission
    {
        get
        {
            if (PhotonActivity.Current is not { } activity) return PermissionState.NotDetermined;
            if (activity.CheckSelfPermission(CameraPermission) == global::Android.Content.PM.Permission.Granted)
                return PermissionState.Granted;
            return Asked(activity) ? PermissionState.Denied : PermissionState.NotDetermined;
        }
    }

    public async ValueTask<ICameraSession?> StartPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (PhotonActivity.Current is not { } activity || Manager is not { } manager) return null;
        if (Choose(manager) is not { } camera) return null;

        if (Permission != PermissionState.Granted)
        {
            // Recorded AFTER the answer, whatever it is: while the prompt is up nobody has refused
            // anything, and a page that reads the permission then must not be told "denied".
            var granted = await activity.RequestPermissionsAsync([CameraPermission], cancellationToken);
            RememberAsked(activity);
            if (!granted) return null;
        }

        return await AndroidCameraSession.OpenAsync(activity, manager, camera, cancellationToken);
    }

    /// <summary>The camera on the back, which is what "the camera" means on a phone; failing that,
    /// whichever the device has.</summary>
    private static string? Choose(CameraManager manager)
    {
        try
        {
            var cameras = manager.GetCameraIdList();
            foreach (var camera in cameras)
            {
                var facing = manager.GetCameraCharacteristics(camera).Get(CameraCharacteristics.LensFacing);
                if (facing is Java.Lang.Integer side && side.IntValue() == (int)LensFacing.Back) return camera;
            }
            return cameras.Length > 0 ? cameras[0] : null;
        }
        catch (CameraAccessException)
        {
            // The camera service is not answering (a device policy disabled it): no camera.
            return null;
        }
    }

    private static bool Asked(Context context) =>
        context.GetSharedPreferences(Preferences, FileCreationMode.Private)?.GetBoolean(AskedKey, false) ?? false;

    private static void RememberAsked(Context context) =>
        context.GetSharedPreferences(Preferences, FileCreationMode.Private)?.Edit()?.PutBoolean(AskedKey, true)?.Apply();
}
