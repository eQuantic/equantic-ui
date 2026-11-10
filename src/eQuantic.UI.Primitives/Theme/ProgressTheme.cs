namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of what shows something is on its way: the ProgressBar, the PullToRefresh and the Skeleton,
/// every member OPTIONAL: a member left null is the component's own default, today's constant. It holds
/// what the component hard-codes that is LOOK; what is behaviour (a threshold, a duration) stays the
/// component's (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
/// <para>
/// <em>How does Flutter solve it?</em> <c>ProgressIndicatorThemeData</c> on <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record ProgressTheme
{
    /// <summary>A progress bar's height (4).</summary>
    public float? BarHeight { get; init; }

    /// <summary>A prominent progress bar's height (8).</summary>
    public float? ProminentBarHeight { get; init; }

    /// <summary>A progress bar's ends (full).</summary>
    public ShapeScale? BarShape { get; init; }

    /// <summary>The track a progress bar fills (the theme's <see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? TrackColor { get; init; }

    /// <summary>The size of the spinner a pull to refresh shows (<see cref="IconSize.Dense"/>, 20).</summary>
    public float? RefreshSpinner { get; init; }

    /// <summary>A skeleton line's height (12).</summary>
    public float? SkeletonLineHeight { get; init; }

    /// <summary>A skeleton block's corners (medium); a line and a circle are fully round.</summary>
    public ShapeScale? SkeletonShape { get; init; }

    /// <summary>A skeleton's fill (the theme's <see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? SkeletonFill { get; init; }

    /// <summary>The glint that crosses a skeleton (the theme's <see cref="IAppTheme.SurfaceHighlight"/>).</summary>
    public ColorToken? SkeletonHighlight { get; init; }
}
