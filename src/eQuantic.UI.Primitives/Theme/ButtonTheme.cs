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
}
