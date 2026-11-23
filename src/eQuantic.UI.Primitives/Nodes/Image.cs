namespace eQuantic.UI.Primitives;

/// <summary>Image fit modes (spec A11).</summary>
public enum ImageFit : byte
{
    /// <summary>Letterboxed inside the slot, whole source visible.</summary>
    Contain = 0,
    /// <summary>Center-crop fill (the default for photos).</summary>
    Cover = 1,
    /// <summary>Distorting fill — reserved for art that tolerates it.</summary>
    Stretch = 2,
}

/// <summary>
/// The Image node (spec A11): an EXPLICITLY sized slot (layout can never infer extent from an
/// undecoded source), fit mode, per-corner radius clipping, and alt text (empty = decorative).
/// v1 fences: NineSlice, the loading/error states and the decode crossfade join with the asset
/// and animation systems; the NATIVE realizer renders a SurfaceSubtle placeholder box under the
/// radius until the engine gains texture upload (the documented text-bars degradation pattern).
/// </summary>
public sealed class Image : VisualNode
{
    public sealed override string NodeKind => "image";

    public Image(string source, float width, float height, ImageFit fit = ImageFit.Cover, string label = "")
    {
        Source = source;
        Width = width;
        Height = height;
        Fit = fit;
        Label = label;
    }

    public string Source { get; }

    /// <summary>
    /// The artwork for DARK mode, when one image cannot serve both — a wordmark drawn in ink for
    /// light surfaces and in white for dark ones. Colors solve this with a
    /// <see cref="ColorToken"/> pair; artwork cannot, so the pair lives here and the realizer
    /// resolves it the same way: no branch in app code, no mode question asked.
    /// <para>Null = one image serves both, which is the common case (photos, avatars).</para>
    /// </summary>
    public string? DarkSource { get; init; }
    public float Width { get; }
    public float Height { get; }
    public ImageFit Fit { get; init; }

    /// <summary>Empty string = decorative (per HTML semantics).</summary>
    public string Label { get; init; }

    /// <summary>Radius token clips via rrect — per-corner allowed (spec A11).</summary>
    public CornerRadii CornerRadius { get; init; }

    /// <summary>
    /// The point of the picture its box is held by, as fractions of the picture from its top-left
    /// corner (0..1 on each axis): the point of the picture at that fraction stands at the same fraction
    /// of the box, so a photograph cropped by <see cref="ImageFit.Cover"/> keeps its subject in the frame
    /// wherever it sits (#1785). The centre, the default, is the crop the fit makes on its own.
    /// <para>
    /// The rule needs nothing of the picture but the fit, which the browser knows before the picture has
    /// loaded: it is the one a page lays out in CSS, and Photon computes it the same way. It is Flutter's
    /// <c>Image.alignment</c> given as a <c>FractionalOffset</c>.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An axis outside 0..1.</exception>
    public Point FocalPoint
    {
        get;
        init
        {
            if (!(value.X is >= 0 and <= 1) || !(value.Y is >= 0 and <= 1))
                throw new ArgumentOutOfRangeException(nameof(FocalPoint), value,
                    "A focal point is a fraction of the picture on each axis, from 0 to 1.");
            field = value;
        }
    } = new(0.5f, 0.5f);

    /// <summary>
    /// How much closer than its fit the picture is drawn, around <see cref="FocalPoint"/>: 1, the default,
    /// is the fit itself, and 1.5 draws it half as large again. The focal point keeps its place in the
    /// box, so a zoomed picture still fills the frame and never shows past its own edge (#1785).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Below 1, or not a finite number.</exception>
    public float Zoom
    {
        get;
        init
        {
            if (!(value >= 1) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(Zoom), value,
                    "A zoom draws the picture at its fit (1) or closer, never farther.");
            field = value;
        }
    } = 1;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
