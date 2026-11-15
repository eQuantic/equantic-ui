namespace eQuantic.UI.Primitives;

/// <summary>The kind of two-dimensional composite a <see cref="Navigable"/> is.</summary>
public enum NavigableRole : byte
{
    /// <summary>A grid of cells the arrows walk in two dimensions — a calendar month, an hour
    /// board (<c>role="grid"</c>, cells are <see cref="PressableRole.GridCell"/>).</summary>
    Grid = 0,

    /// <summary>A TREE whose rows are its VISIBLE items in reading order (<c>role="tree"</c>, items are
    /// <see cref="PressableRole.TreeItem"/>). The moves keep their names and the composite gives each
    /// its tree meaning: the rows are Up and Down, the items Left and Right (close or go up, open or go
    /// in), the row's bounds Home and End. Its rows are not wrapped in rows, which a tree must not
    /// hold. Appended (#1529): an enum's values are an ABI.</summary>
    Tree = 1,
}
