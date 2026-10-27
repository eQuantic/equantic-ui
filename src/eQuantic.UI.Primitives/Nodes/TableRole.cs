namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="TableViewport"/> is to a reader (#1081). Flutter's <c>SemanticsRole</c> says
/// <c>table</c> and each platform says table and grid; a group says nothing of rows or columns.
/// </summary>
public enum TableRole
{
    /// <summary>A group of what its cells hold, which says nothing of rows or columns.</summary>
    Group,

    /// <summary>A table of rows and cells, read in place.</summary>
    Table,

    /// <summary>A grid of rows and cells a user moves through and selects in, one cell current.</summary>
    Grid,
}
