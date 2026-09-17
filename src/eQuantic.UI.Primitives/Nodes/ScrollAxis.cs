namespace eQuantic.UI.Primitives;

/// <summary>Scroll axis (spec A6).</summary>
public enum ScrollAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
    /// <summary>Spec S7: scrolls on both axes (tables, canvases).</summary>
    Both = 2,
}
