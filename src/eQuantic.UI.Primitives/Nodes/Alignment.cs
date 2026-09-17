namespace eQuantic.UI.Primitives;

/// <summary>Nine-position alignment for <see cref="Stack"/> children (spec A3).</summary>
public enum Alignment : byte
{
    TopStart = 0, TopCenter = 1, TopEnd = 2,
    CenterStart = 3, Center = 4, CenterEnd = 5,
    BottomStart = 6, BottomCenter = 7, BottomEnd = 8,
}
