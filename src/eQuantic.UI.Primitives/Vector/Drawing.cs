namespace eQuantic.UI.Primitives;

/// <summary>
/// A piece of vector ARTWORK on screen — a logo, an illustration, a mark a designer drew — placed
/// at a size the author decides and drawn from <see cref="VectorDrawing"/> data.
/// <para>
/// The web lowers it to an inline <c>&lt;svg&gt;</c> with one <c>&lt;path&gt;</c> per shape, so it
/// stays in the DOM as vector: it scales without blurring, and the parts the file left as
/// <c>currentColor</c> follow whatever tints them. Photon rasterizes each shape at the box it was
/// given and paints it in its own colour. Neither target reads a file, which is the whole reason
/// the artwork arrives as data.
/// </para>
/// <para>
/// Contrast with its two neighbours. <see cref="Icon"/> is a glyph from the §07 size whitelist;
/// <see cref="Vector"/> is one path in one tint at any size. This is the multi-shape, multi-colour
/// one — the node you point at a <c>.svg</c>.
/// </para>
/// </summary>
public sealed class Drawing : VisualNode
{
    public sealed override string NodeKind => "drawing";

    /// <param name="artwork">The drawing's shapes and its own grid — what a <c>.svg</c> file became.</param>
    /// <param name="width">The box's WIDTH in dp.</param>
    /// <param name="height">
    /// The box's height. Omitted (or 0) it comes from the artwork's own aspect, which is the
    /// difference between this and <see cref="Vector"/>: an icon defaults to a square because that
    /// is what an icon is, and a logo defaults to ITS shape, because a squashed logo is a wrong
    /// logo. Give both to letterbox or stretch deliberately.
    /// </param>
    public Drawing(VectorDrawing artwork, float width, float height = 0, ColorToken? tint = null,
        string? label = null)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "A drawing needs a positive width.");
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height), "A drawing's height cannot be negative.");
        Artwork = artwork;
        Width = width;
        Height = height > 0 ? height : width / (artwork.Aspect <= 0 ? 1 : artwork.Aspect);
        Tint = tint;
        Label = label;
    }

    public VectorDrawing Artwork { get; }

    public float Width { get; }

    /// <summary>The box's height — derived from the artwork's aspect unless the author decided.</summary>
    public float Height { get; }

    /// <summary>
    /// What answers the shapes the file left as <c>currentColor</c>. Null inherits the context text
    /// colour, exactly like <see cref="Text"/> and <see cref="Icon"/> — so a monochrome mark
    /// follows the theme without the app branching, and a full-colour one ignores this entirely
    /// because none of its shapes asked.
    /// </summary>
    public ColorToken? Tint { get; init; }

    /// <summary>Accessibility label; null = decorative (aria-hidden on web).</summary>
    public string? Label { get; init; }
}
