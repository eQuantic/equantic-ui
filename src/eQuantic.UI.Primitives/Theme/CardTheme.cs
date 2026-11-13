namespace eQuantic.UI.Primitives;

/// <summary>
/// The Card's slot,
/// every member OPTIONAL: a member left null is the component's own default, today's constant. It holds
/// what the component hard-codes that is LOOK; what is behaviour (a threshold, a duration) stays the
/// component's (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10).
/// <para>
/// <em>How does Flutter solve it?</em> <c>CardThemeData</c> on <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record CardTheme
{
    /// <summary>A card's corners (large).</summary>
    public ShapeScale? Shape { get; init; }

    /// <summary>An outlined or elevated card's border (1).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>An elevated card's elevation (1).</summary>
    public int? Elevation { get; init; }

    /// <summary>A filled card's fill (the theme's <see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? FilledColor { get; init; }

    /// <summary>An elevated card's border: none in light, the theme's border in dark.</summary>
    public ColorToken? ElevatedBorderColor { get; init; }

    /// <summary>
    /// <paramref name="over"/> laid over <paramref name="under"/>, member by member: a member the over
    /// sets wins, and one it leaves null is the under's. Null only when both are, which is what a theme
    /// built with <c>With</c> answers for a slot neither theme sets.
    /// </summary>
    public static CardTheme? Merge(CardTheme? under, CardTheme? over) =>
        under is null ? over : over is null ? under : new CardTheme
        {
            Shape = over.Shape ?? under.Shape,
            BorderWidth = over.BorderWidth ?? under.BorderWidth,
            Elevation = over.Elevation ?? under.Elevation,
            FilledColor = over.FilledColor ?? under.FilledColor,
            ElevatedBorderColor = over.ElevatedBorderColor ?? under.ElevatedBorderColor,
        };
}
