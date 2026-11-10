namespace eQuantic.UI.Primitives;

/// <summary>
/// A number per <see cref="Density"/>, each side optional, for the slots of a control that varies by
/// density alone (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): a checkbox, a radio and a switch have one
/// size each, with a compact side. Both sides cross to the browser, since the density is the browser's
/// to report. A side left null is the component's own value at that density.
/// </summary>
public sealed record DensityValue
{
    /// <summary>The value at <see cref="Density.Comfortable"/>.</summary>
    public float? Comfortable { get; init; }

    /// <summary>The value at <see cref="Density.Compact"/>.</summary>
    public float? Compact { get; init; }
}
