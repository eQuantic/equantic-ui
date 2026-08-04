namespace eQuantic.UI.Primitives;

/// <summary>
/// A LIVE camera surface — the vocabulary's first node whose pixels the engine does not draw from
/// a display list alone. The web realizes it as a video element playing the session's stream; the
/// Photon realizer uploads the session's latest frame as a texture, once per frame, into the same
/// GPU slot.
/// <para>
/// Explicitly sized like <see cref="Image"/>, and for the same reason: layout can never infer an
/// extent from a source that has not answered yet. A null <see cref="Session"/> renders the
/// SurfaceSubtle placeholder under the radius — the state every page is in before
/// <see cref="ICamera.StartPreviewAsync"/> resolves, so "not started yet" needs no branching in
/// app code.
/// </para>
/// </summary>
public sealed class CameraPreview : VisualNode
{
    public sealed override string NodeKind => "cameraPreview";

    public CameraPreview(ICameraSession? session, float width, float height)
    {
        Session = session;
        Width = width;
        Height = height;
    }

    /// <summary>The running feed, or null while there is none — the placeholder state.</summary>
    public ICameraSession? Session { get; }

    public float Width { get; }
    public float Height { get; }

    /// <summary>Radius clips via rrect, exactly as <see cref="Image"/> does.</summary>
    public CornerRadii CornerRadius { get; init; }

    /// <summary>What assistive tech reads for a surface it cannot describe. Empty = decorative.</summary>
    public string Alt { get; init; } = "";
}
