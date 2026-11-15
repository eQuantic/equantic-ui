using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// One node of a <see cref="TreeView"/>: its <see cref="Key"/>, unique in the tree and what the tree
/// remembers it by (open, loaded, active), and the <see cref="Label"/> it shows and is read by.
/// <para>
/// Its children are <see cref="Children"/> when the app has them. When it does not, they are loaded
/// the first time the node opens, through the tree's loader; a node that never has any says so with
/// <see cref="IsLeaf"/>, and shows no chevron.
/// </para>
/// </summary>
public sealed record TreeNode(string Key, string Label)
{
    /// <summary>The glyph before the label, a folder's or a file's; none when null.</summary>
    public IconGlyph? Icon { get; init; }

    /// <summary>A node that never has children: no chevron, nothing to open, nothing to load.</summary>
    public bool IsLeaf { get; init; }

    /// <summary>
    /// The node's children, in the order they show. Null means "not known yet": the tree loads them when
    /// the node first opens. Given, they win over anything loaded before, which is how an app that read
    /// its tree again after a change shows the change.
    /// </summary>
    public IReadOnlyList<TreeNode>? Children { get; init; }
}
