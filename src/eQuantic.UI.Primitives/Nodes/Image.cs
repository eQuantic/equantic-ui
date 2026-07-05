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

    public Image(string source, float width, float height, ImageFit fit = ImageFit.Cover, string alt = "")
    {
        Source = source;
        Width = width;
        Height = height;
        Fit = fit;
        Alt = alt;
    }

    public string Source { get; }
    public float Width { get; }
    public float Height { get; }
    public ImageFit Fit { get; init; }

    /// <summary>Empty string = decorative (per HTML semantics).</summary>
    public string Alt { get; init; }

    /// <summary>Radius token clips via rrect — per-corner allowed (spec A11).</summary>
    public CornerRadii CornerRadius { get; init; }
}
