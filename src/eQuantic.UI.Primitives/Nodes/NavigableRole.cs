namespace eQuantic.UI.Primitives;

/// <summary>The kind of two-dimensional composite a <see cref="Navigable"/> is.</summary>
public enum NavigableRole : byte
{
    /// <summary>A grid of cells the arrows walk in two dimensions — a calendar month, an hour
    /// board (<c>role="grid"</c>, cells are <see cref="PressableRole.GridCell"/>).</summary>
    Grid = 0,
}
