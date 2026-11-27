namespace eQuantic.UI.Primitives;

/// <summary>Where an <see cref="Anchored"/> panel attaches relative to its anchor: the four corner placements and the
/// two centred ones. A panel a placement would put past the viewport's start or end edge is moved back inside
/// (<see cref="Anchored.ViewportInset"/>, #1933); a vertical flip is the positioning fence.</summary>
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
