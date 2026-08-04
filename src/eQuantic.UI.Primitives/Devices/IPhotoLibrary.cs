namespace eQuantic.UI.Primitives;

/// <summary>
/// The pictures the user already has. Every target has a system picker for this and NONE of them
/// should be reimplemented: the picker is where the user's own privacy expectations live — on iOS
/// and Android the modern pickers hand over exactly the chosen photos without the app ever seeing
/// the library, which is why asking through them is better than asking for the permission.
/// </summary>
public interface IPhotoLibrary
{
    /// <summary>Whether this device has one at all.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// What the user has decided. Reading it never prompts — that is the point: an app can show the
    /// right thing (a button, or an explanation with a way to Settings) before asking anything.
    /// </summary>
    ValueTask<PermissionState> GetPermissionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows the system's picker and returns what the user chose, or NULL when they chose nothing —
    /// which includes cancelling and includes refusing. One call, and the permission flow is inside
    /// it: there is no separate request step for an app author to forget, and no state machine to
    /// get wrong.
    /// </summary>
    ValueTask<ImageData?> PickImageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The same, for more than one. An empty list means the user chose nothing.
    /// <paramref name="limit"/> zero means no limit the app imposes.
    /// </summary>
    ValueTask<IReadOnlyList<ImageData>> PickImagesAsync(int limit = 0,
        CancellationToken cancellationToken = default);
}
