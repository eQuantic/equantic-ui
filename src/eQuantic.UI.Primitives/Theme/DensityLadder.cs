namespace eQuantic.UI.Primitives;

/// <summary>
/// A <see cref="SizeLadder"/> per <see cref="Density"/>, for a number that moves with the control's size
/// AND with the target's density (a medium control is 40 under a thumb and 32 under a pointer). Both
/// ride in the theme's JSON, since the density is the browser's to report and can change after the
/// server wrote the page. A side left null is the next step of the slot order's.
/// </summary>
public sealed record DensityLadder
{
    /// <summary>Under a thumb, <see cref="Density.Comfortable"/>.</summary>
    public SizeLadder? Comfortable { get; init; }

    /// <summary>Under a pointer, <see cref="Density.Compact"/>.</summary>
    public SizeLadder? Compact { get; init; }
}
