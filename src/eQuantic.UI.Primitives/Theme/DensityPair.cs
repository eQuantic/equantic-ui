namespace eQuantic.UI.Primitives;

/// <summary>
/// A number per <see cref="Density"/>, each side optional, for the slots of a control that varies by
/// density alone (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a checkbox, a radio and a switch have one
/// size each, with a compact side. Both sides cross to the browser, since the density is the browser's
/// to report. A side left null is the component's own value at that density.
/// </summary>
public sealed record DensityPair
{
    /// <summary>The value at <see cref="Density.Comfortable"/>.</summary>
    public float? Comfortable { get; init; }

    /// <summary>The value at <see cref="Density.Compact"/>.</summary>
    public float? Compact { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how a slot that holds one is merged.
    /// </summary>
    public static DensityPair? Merge(DensityPair? under, DensityPair? over) =>
        under is null ? over : over is null ? under : new DensityPair
        {
            Comfortable = over.Comfortable ?? under.Comfortable,
            Compact = over.Compact ?? under.Compact,
        };
}
