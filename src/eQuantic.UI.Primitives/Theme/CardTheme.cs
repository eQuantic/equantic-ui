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
}
