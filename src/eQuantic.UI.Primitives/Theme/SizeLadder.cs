namespace eQuantic.UI.Primitives;

/// <summary>
/// A value per <see cref="SizeVariant"/>, each OPTIONAL: the data a theme slot holds for a number that
/// moves with a control's size (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10). A rung left null is the next
/// step of the slot order's, so a theme that changes how tall a medium control is names the medium rung
/// alone. Data and no delegate, because a slot crosses to the browser in the theme's JSON.
/// </summary>
public sealed record SizeLadder
{
    public float? Small { get; init; }
    public float? Medium { get; init; }
    public float? Large { get; init; }
    public float? XLarge { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s rungs laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static SizeLadder? Merge(SizeLadder? under, SizeLadder? over) =>
        under is null ? over : over is null ? under : new SizeLadder
        {
            Small = over.Small ?? under.Small,
            Medium = over.Medium ?? under.Medium,
            Large = over.Large ?? under.Large,
            XLarge = over.XLarge ?? under.XLarge,
        };
}
