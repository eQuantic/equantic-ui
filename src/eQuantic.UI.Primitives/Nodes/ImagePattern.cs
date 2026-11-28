namespace eQuantic.UI.Primitives;

/// <summary>
/// A raster image repeated behind a box (#1937): linen, paper, a photograph's grain. It takes the
/// source an <see cref="Image"/> takes, and the same dark pair, so an app names its artwork once for
/// both nodes.
/// <para>
/// A raster tile is sized in dp, by a width, a height or a fit, never by its pixels; supply at least
/// the tile's dp times the highest scale the app ships to. It has no opacity of its own, because no
/// target's background can fade one raster layer: the image carries its own transparency, so a
/// quieter tile is an image exported with the alpha it wants.
/// </para>
/// </summary>
public sealed record ImagePattern : ArtworkPattern
{
    /// <param name="source">The image each tile repeats, as <see cref="Image"/> takes it.</param>
    /// <param name="width">The tile's width: dp, a fraction of the placing area, or auto.</param>
    /// <param name="height">The tile's height: dp, a fraction of the placing area, or auto.</param>
    /// <exception cref="ArgumentOutOfRangeException">An empty source.</exception>
    public ImagePattern(string source, TileLength width = default, TileLength height = default) : base(width, height)
    {
        if (string.IsNullOrEmpty(source))
            throw new ArgumentOutOfRangeException(nameof(source), source, EmptySource);
        Source = source;
    }

    /// <summary>The image each tile repeats.</summary>
    public string Source { get; }

    /// <summary>
    /// The image for DARK mode, when one cannot serve both, resolved as <see cref="Image.DarkSource"/>
    /// is. <c>null</c> = <see cref="Source"/> serves both.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An empty source.</exception>
    public string? DarkSource
    {
        get;
        init
        {
            if (value is { Length: 0 })
                throw new ArgumentOutOfRangeException(nameof(DarkSource), value, EmptySource);
            field = value;
        }
    }

    private const string EmptySource = "A tile's image source names the image it repeats, so it is never empty.";

    private protected override TResult Dispatch<TState, TResult>(IPatternVisitor<TState, TResult> visitor, TState state) =>
        visitor.Visit(this, state);
}
