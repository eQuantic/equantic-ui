namespace eQuantic.UI.Primitives;

/// <summary>
/// A piece of artwork placed in a box and repeated (#1937): what <see cref="DrawingPattern"/> and
/// <see cref="ImagePattern"/> share, its size, its repetition per axis and its position.
/// <para>
/// The size is a width and a height (<see cref="TileLength"/>), the missing one from the artwork's
/// aspect, or a <see cref="Fit"/> against the placing area, never the artwork's own pixels. The tile
/// whose point at <see cref="Anchor"/> stands at the same fraction of the placing area, moved by
/// <see cref="Offset"/>, is where the repetition starts, and it continues on every side from there.
/// </para>
/// </summary>
public abstract record ArtworkPattern : Pattern
{
    private protected ArtworkPattern(TileLength width, TileLength height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>The tile's width: dp, a fraction of the placing area's, or auto.</summary>
    public TileLength Width { get; }

    /// <summary>The tile's height: dp, a fraction of the placing area's, or auto.</summary>
    public TileLength Height { get; }

    /// <summary>
    /// The artwork fitted to the placing area instead of sized by a width or a height:
    /// <see cref="ImageFit.Contain"/>, <see cref="ImageFit.Cover"/> or <see cref="ImageFit.Stretch"/>.
    /// <c>null</c> (the default) sizes the tile by its lengths.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A fit beside a width or a height.</exception>
    public ImageFit? Fit
    {
        get;
        init
        {
            if (value is not null && !(Width.IsAuto && Height.IsAuto))
                throw new ArgumentOutOfRangeException(nameof(Fit), value,
                    "A tile is sized by a fit or by its lengths, never both: a fit beside a width or a height has nothing left to decide.");
            field = value;
        }
    }

    /// <summary>How the tile repeats across: <see cref="TileRepeat.Repeat"/> by default.</summary>
    public TileRepeat HorizontalRepeat { get; init; }

    /// <summary>How the tile repeats down: <see cref="TileRepeat.Repeat"/> by default.</summary>
    public TileRepeat VerticalRepeat { get; init; }

    /// <summary>
    /// Where the first tile stands, as fractions on each axis (0..1): the tile's point at this
    /// fraction is placed at the same fraction of the placing area, the rule
    /// <see cref="Image.FocalPoint"/> follows. The top-left corner, (0, 0), by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An axis outside 0..1.</exception>
    public Point Anchor
    {
        get;
        init
        {
            if (!(value.X is >= 0 and <= 1) || !(value.Y is >= 0 and <= 1))
                throw new ArgumentOutOfRangeException(nameof(Anchor), value,
                    "A tile's anchor is a fraction of the area it is placed in on each axis, from 0 to 1.");
            field = value;
        }
    }

    /// <summary>A shift of the anchored tile, in dp, added after <see cref="Anchor"/> placed it.</summary>
    public Point Offset { get; init; }

    /// <summary>Whether the tile is sized at all: by a length or by a fit.</summary>
    internal bool IsSized => Fit is not null || !Width.IsAuto || !Height.IsAuto;

    /// <summary>
    /// The tile's size in dp, inside a placing area of <paramref name="area"/>, for artwork whose width is
    /// <paramref name="aspect"/> times its height: two lengths as they are, one length and the other from
    /// the aspect, or the artwork fitted to the area. The one answer for every target that sizes a tile
    /// itself; the web writes the lengths and leaves the browser to size it.
    /// </summary>
    /// <exception cref="InvalidOperationException">A tile with neither a length nor a fit.</exception>
    public Size SizeIn(Size area, float aspect)
    {
        var shape = aspect > 0 && float.IsFinite(aspect) ? aspect : 1;
        if (Fit is { } fit)
        {
            if (fit == ImageFit.Stretch) return area;
            var wider = area.Height <= 0 || area.Width / area.Height > shape;
            // Contain meets the tighter side, cover the looser one.
            var byHeight = fit == ImageFit.Contain ? wider : !wider;
            return byHeight
                ? new Size(area.Height * shape, area.Height)
                : new Size(area.Width, area.Width / shape);
        }
        if (!Width.IsAuto && !Height.IsAuto) return new Size(Width.In(area.Width), Height.In(area.Height));
        if (!Width.IsAuto)
        {
            var width = Width.In(area.Width);
            return new Size(width, width / shape);
        }
        if (!Height.IsAuto)
        {
            var height = Height.In(area.Height);
            return new Size(height * shape, height);
        }
        throw new InvalidOperationException(Unsized);
    }

    /// <summary>Why a tile with neither a length nor a fit is refused: a tile is never sized by its
    /// artwork's own pixels, which are not dp.</summary>
    internal const string Unsized =
        "A tile needs a width, a height or a Fit: it is never sized by its artwork's own pixels, which are not dp.";
}
