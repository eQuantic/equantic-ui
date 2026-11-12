namespace eQuantic.UI.Primitives;

/// <summary>
/// The Button's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): what makes a button look like itself and
/// was written in the component, every member OPTIONAL. A member left null is the next step of the slot
/// order: for a number of the ladder, the button's own <see cref="Controls"/>, then the theme's
/// <see cref="IAppTheme.Controls"/>, then <see cref="Sizing"/>; for the rest, the component's default. It
/// styles every Button, the ones a Dialog, a Banner, a Pagination and a form build included.
/// <para>
/// <em>How does Flutter solve it?</em> <c>ButtonThemeData</c> and <c>ButtonStyle</c> on <c>ThemeData</c>:
/// nullable members the widget resolves against its defaults.
/// </para>
/// </summary>
public sealed record ButtonTheme
{
    /// <summary>A ladder for buttons alone, ahead of the theme's <see cref="IAppTheme.Controls"/>.</summary>
    public ControlsTheme? Controls { get; init; }

    /// <summary>The width a button never shrinks below (64, spec A12).</summary>
    public float? MinWidth { get; init; }

    /// <summary>The label's weight (semi-bold).</summary>
    public FontWeight? LabelWeight { get; init; }

    /// <summary>The label's tracking (0.1).</summary>
    public float? LabelTracking { get; init; }

    /// <summary>The outline variant's border width (1).</summary>
    public float? OutlineWidth { get; init; }

    /// <summary>The link variant's horizontal padding, which has no fill to pad (6).</summary>
    public float? LinkPaddingX { get; init; }

    /// <summary>The outline variant's border (<see cref="IAppTheme.BorderStrong"/>). The colours a variant
    /// fills with are <see cref="IAppTheme.Colors"/>'; a slot carries the ones the button chooses itself.</summary>
    public ColorToken? OutlineColor { get; init; }

    /// <summary>The fill an outline or a ghost button takes when hovered or pressed
    /// (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? QuietFill { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static ButtonTheme? Merge(ButtonTheme? under, ButtonTheme? over) =>
        under is null ? over : over is null ? under : new ButtonTheme
        {
            Controls = ControlsTheme.Merge(under.Controls, over.Controls),
            MinWidth = over.MinWidth ?? under.MinWidth,
            LabelWeight = over.LabelWeight ?? under.LabelWeight,
            LabelTracking = over.LabelTracking ?? under.LabelTracking,
            OutlineWidth = over.OutlineWidth ?? under.OutlineWidth,
            LinkPaddingX = over.LinkPaddingX ?? under.LinkPaddingX,
            OutlineColor = over.OutlineColor ?? under.OutlineColor,
            QuietFill = over.QuietFill ?? under.QuietFill,
        };
}
