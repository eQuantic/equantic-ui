namespace eQuantic.UI.Primitives;

/// <summary>
/// A vector drawing repeated behind a box (#1937): the brand's tile under a desk, quiet behind the
/// sheets. The web writes it as an SVG layer of the box's background, Photon composites it once at
/// the device's scale and repeats it in one draw.
/// <code>
/// Patterns = [new DrawingPattern(Vectors.PatternTile, 330) { Opacity = 0.09f }]
/// </code>
/// </summary>
public sealed record DrawingPattern : ArtworkPattern
{
    /// <param name="artwork">The drawing each tile repeats, the one a <see cref="Drawing"/> node shows.</param>
    /// <param name="width">The tile's width: dp, a fraction of the placing area, or auto.</param>
    /// <param name="height">The tile's height: dp, a fraction of the placing area, or auto.</param>
    /// <param name="tint">What the shapes the artwork leaves as <c>currentColor</c> are painted in;
    /// <c>null</c> paints them in the theme's <c>TextPrimary</c>.</param>
    /// <exception cref="ArgumentNullException">No artwork.</exception>
    public DrawingPattern(VectorDrawing artwork, TileLength width = default, TileLength height = default,
        ColorToken? tint = null) : base(width, height)
    {
        Artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
        Tint = tint;
    }

    /// <summary>The drawing each tile repeats.</summary>
    public VectorDrawing Artwork { get; }

    /// <summary>The colour of the artwork's <c>currentColor</c> shapes; <c>null</c> = the theme's
    /// <c>TextPrimary</c>.</summary>
    public ColorToken? Tint { get; }

    /// <summary>
    /// How much of the tile shows, 0..1: the artwork is painted as ONE picture at this opacity, so
    /// overlapping shapes inside it do not blend twice. Fully opaque by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Outside 0..1.</exception>
    public float Opacity
    {
        get;
        init
        {
            if (!(value is >= 0 and <= 1))
                throw new ArgumentOutOfRangeException(nameof(Opacity), value,
                    "A tile's opacity is a fraction from 0 (nothing shows) to 1 (fully opaque).");
            field = value;
        }
    } = 1;

    private protected override TResult Dispatch<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
