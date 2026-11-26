namespace eQuantic.UI.Primitives;

/// <summary>
/// The look a box hands the text and icons inside it when they name none of their own: Flutter's
/// <c>DefaultTextStyle</c> and <c>IconTheme</c>, whose colour half is <see cref="BoxStyle.Foreground"/> (#614).
/// A component states it on the box it draws, so content the developer hands it as a node, a bare
/// <c>Text("Save")</c> or a <c>string</c>, is drawn in the component's own type, on its own line limit, at its
/// own icon size (#1577).
/// <para>
/// Every member is optional and means "the one around this box": a box inside another lays its own over the outer
/// one member by member (<see cref="Merge"/>), and where no box states a member a text is drawn in
/// <see cref="TypeRole.BodyL"/>, with no line limit, at the start, and an icon at 24.
/// </para>
/// </summary>
public sealed record ContentStyle
{
    /// <summary>The role a <see cref="Text"/> that names none is set in.</summary>
    public TypeRole? Role { get; init; }

    /// <summary>
    /// The type style a <see cref="Text"/> that names none takes, over <see cref="Role"/>'s: a button's label size by
    /// its size, its weight and tracking. It reaches only a text that took this look's role, because a style was
    /// written for its role; a text that names its own role keeps that role's type whole.
    /// </summary>
    public TypeStyle? Type { get; init; }

    /// <summary>The line limit a <see cref="Text"/> that names none is cut to; 0 is no limit.</summary>
    public int? MaxLines { get; init; }

    /// <summary>The line alignment a <see cref="Text"/> that names none takes.</summary>
    public TextAlignment? Align { get; init; }

    /// <summary>
    /// The size an <see cref="Icon"/> that names none takes, in dp, from the §07 whitelist (16, 20, 24 or 32), which
    /// an <see cref="Icon"/> holds itself to.
    /// </summary>
    public float? IconSize
    {
        get => _iconSize;
        init => _iconSize = value is null or 16 or 20 or 24 or 32 ? value
            : throw new ArgumentOutOfRangeException(nameof(IconSize), value,
                "An icon's size is on the §07 whitelist (16/20/24/32), here as on Icon itself.");
    }

    private readonly float? _iconSize;

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/> names
    /// wins, what it leaves null is <paramref name="under"/>'s. Null when both are. How a box's look meets the look
    /// around it, as Flutter's <c>DefaultTextStyle.merge</c>.
    /// </summary>
    public static ContentStyle? Merge(ContentStyle? under, ContentStyle? over) =>
        under is null ? over : over is null ? under : new ContentStyle
        {
            Role = over.Role ?? under.Role,
            // A style rides the role it was written for: an inner box that names a role without a style must not
            // inherit a style cut for the outer box's role.
            Type = over.Type ?? (over.Role is null ? under.Type : null),
            MaxLines = over.MaxLines ?? under.MaxLines,
            Align = over.Align ?? under.Align,
            IconSize = over.IconSize ?? under.IconSize,
        };
}
