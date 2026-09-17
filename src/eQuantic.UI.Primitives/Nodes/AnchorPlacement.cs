namespace eQuantic.UI.Primitives;

/// <summary>Where an <see cref="Anchored"/> panel attaches relative to its anchor (wave 3 v1: the
/// four corner placements; centered variants and viewport flip/clamp are the positioning fence).</summary>
public enum AnchorPlacement : byte
{
    BottomStart = 0,
    BottomEnd = 1,
    TopStart = 2,
    TopEnd = 3,
    /// <summary>Wave 3b: centered under the anchor (tooltips, hints).</summary>
    BottomCenter = 4,
    /// <summary>Wave 3b: centered above the anchor (the tooltip default).</summary>
    TopCenter = 5,
}
