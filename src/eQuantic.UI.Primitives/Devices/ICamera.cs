namespace eQuantic.UI.Primitives;

/// <summary>
/// A running camera feed — what a <see cref="CameraPreview"/> node shows and what a still is
/// captured from. Disposing stops the hardware; the light next to the lens is the user's evidence,
/// and a session an app forgets to dispose keeps it on.
/// </summary>
public interface ICameraSession : IDisposable
{
    /// <summary>Names this session for realizers that attach by reference rather than by pixels —
    /// the web's video element finds its MediaStream with this.</summary>
    string Id { get; }

    /// <summary>The latest frame's size, or (0,0) before the first frame arrives.</summary>
    int FrameWidth { get; }
    int FrameHeight { get; }

    /// <summary>Straight sRGB RGBA bytes of the LATEST frame, written into the same array each
    /// time. Null on targets that never hand pixels over (the web shows the stream directly).</summary>
    byte[]? FrameBytes { get; }

    /// <summary>Bumped once per delivered frame — a renderer re-reads when the number moved and a
    /// still capture waits for it to move at all.</summary>
    int FrameVersion { get; }

    /// <summary>The latest frame as an encoded image, or null before the first frame. This is the
    /// still-capture path: what the preview shows is what the capture takes.</summary>
    ImageData? Capture();
}

/// <summary>
/// The camera — the one capability whose answer is a PICTURE THAT MOVES, which is why it comes
/// with its own vocabulary node: a live surface is not a rect the engine can draw from a display
/// list alone, and <see cref="CameraPreview"/> is how a page composites one anyway.
/// <para>
/// Asking IS the permission flow, as everywhere in this vocabulary: <see cref="StartPreviewAsync"/>
/// prompts when the user was never asked, and resolves null when it ends up denied —
/// <see cref="Permission"/> says which, so the page can point at Settings instead of asking again.
/// </para>
/// </summary>
public interface ICamera
{
    /// <summary>Whether this device has a camera at all.</summary>
    bool IsAvailable { get; }

    /// <summary>The current answer to "may this app watch". Never prompts.</summary>
    PermissionState Permission { get; }

    /// <summary>Starts the feed, prompting first if needed. Null when permission ends up denied or
    /// no camera answers. The session keeps the hardware running until disposed.</summary>
    ValueTask<ICameraSession?> StartPreviewAsync(CancellationToken cancellationToken = default);
}
