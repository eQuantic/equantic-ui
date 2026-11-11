using System.Globalization;

namespace eQuantic.UI.Web;

/// <summary>
/// A node's place in the tree as both producers spell it: the browser's lowering threads it through every node
/// (<c>lowering.ts</c>, its <c>path</c>), and the server's follows the same steps (<see cref="WebLoweringVisitor"/>).
/// Each container names the step to each child by one of these segments, the same one on both sides, so what a
/// node is scoped by is the same on the server's first pass and after hydration, with nothing counted.
/// <para>
/// A table's cells are what it scopes today: two tables that shared a label, two spreadsheets or two unlabelled
/// tables, wrote the same cell ids, and a reader in the second grid heard the first grid's values (#1115).
/// </para>
/// </summary>
internal static class LoweringPath
{
    /// <summary>Where a lowering starts. The browser starts each root of a reconciler pass at <c>r</c> and the
    /// pass's count, and outside a pass at <c>r</c> alone, which the server cannot know: what is scoped by a
    /// path reads it below its root (<see cref="Scope"/>).</summary>
    public const string Root = "r";

    /// <summary>The one child of a node that holds one, and what a component builds.</summary>
    public const string Child = "0";

    /// <summary>The second child of a node that holds two: a code surface's options, an anchored node's panel.</summary>
    public const string Second = "1";

    /// <summary>The scrim an anchored node draws behind its panel to dismiss it.</summary>
    public const string Scrim = "s";

    /// <summary>The child at <paramref name="index"/> of a list: a flex's, a stack's or a grid's children, a
    /// navigable grid's rows, an adaptive node's arms (compact, medium, expanded).</summary>
    public static string Item(int index) => index.ToString(CultureInfo.InvariantCulture);

    /// <summary>A table's cell, by its key, its position, rather than its index, which the window moves: or, for a
    /// cell with none (a mark), <c>m</c> and its index among the table's cells.</summary>
    public static string Cell(string? key, int index) => key ?? "m" + Item(index);

    /// <summary>A code surface's note at <paramref name="index"/>.</summary>
    public static string Note(int index) => "note/" + Item(index);

    /// <summary>The path one step below <paramref name="parent"/>.</summary>
    public static string Below(string parent, string segment) => parent + "/" + segment;

    /// <summary>
    /// What a node is scoped by: its path below its root, the same on both sides whichever root the browser's
    /// pass gave it. A table's cells' ids hash it.
    /// </summary>
    public static string Scope(string path)
    {
        var below = path.IndexOf('/');
        return below < 0 ? "" : path[below..];
    }
}
