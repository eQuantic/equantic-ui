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
    /// <param name="width">
    /// The box's WIDTH: dp (a number converts), or <see cref="SizeValue.Fill"/> for the width its
    /// parent offers — a map at the width of its column, which no number can say for every window.
    /// </param>
    /// <param name="height">
    /// The box's height in dp. Omitted (or 0) it comes from the artwork's own aspect, which is the
    /// difference between this and <see cref="Vector"/>: an icon defaults to a square because that
    /// is what an icon is, and a logo defaults to ITS shape, because a squashed logo is a wrong
    /// logo. Give both to letterbox or stretch deliberately.
    /// </param>
    /// <param name="tint">One token over the whole artwork (alpha-mask semantics), or null to keep its own colours.</param>
    /// <param name="label">What assistive tech announces, or null for artwork that carries no meaning of its own.</param>
    public Drawing(VectorDrawing artwork, SizeValue width, float height = 0, ColorToken? tint = null,
        string? label = null)
    {
        if (width.Kind == SizeKind.Fixed && width.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "A drawing needs a positive width.");
        if (width.Kind is SizeKind.Hug)
            throw new ArgumentOutOfRangeException(nameof(width),
                "A drawing has no content to hug: give it a width in dp, or SizeValue.Fill for its parent's.");
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height), "A drawing's height cannot be negative.");
        Artwork = artwork;
        Width = width;
        Height = height;
        Tint = tint;
        Label = label;
    }

    public VectorDrawing Artwork { get; }

    /// <summary>The box's width: dp, or a fill of the width the parent offers.</summary>
    public SizeValue Width { get; }

    /// <summary>The height the author decided, in dp; 0 when the artwork's aspect decides it.</summary>
    public float Height { get; }

    /// <summary>Width over height, the ratio a derived height keeps (1 for an artwork with no height).</summary>
    public float Aspect => Artwork.Aspect <= 0 ? 1 : Artwork.Aspect;

    /// <summary>The height this drawing takes at <paramref name="width"/> dp: the decided one, or the aspect's.</summary>
    public float HeightAt(float width) => Height > 0 ? Height : width / Aspect;

    /// <summary>
    /// What answers the shapes the file left as <c>currentColor</c>. Null inherits the context text
    /// colour, exactly like <see cref="Text"/> and <see cref="Icon"/> — so a monochrome mark
    /// follows the theme without the app branching, and a full-colour one ignores this entirely
    /// because none of its shapes asked.
    /// </summary>
    public ColorToken? Tint { get; init; }

    /// <summary>Accessibility label; null = decorative (aria-hidden on web).</summary>
    public string? Label { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
