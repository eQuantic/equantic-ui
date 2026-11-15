namespace eQuantic.UI.Components;

/// <summary>
/// One VISIBLE row of a <see cref="TreeView"/>, in the order the tree draws them: a node, or the status
/// item that stands under a node while its children load or after the load failed.
/// <para>
/// <see cref="Key"/> is the node's key, or for a status item the key of the node it stands under;
/// <see cref="Level"/>, <see cref="Position"/> and <see cref="Count"/> are the item's place, from one,
/// as <c>TreePlace</c> states it.
/// </para>
/// </summary>
internal sealed record TreeViewRow(string Key, TreeNode? Node, string? ParentKey, int Level, int Position, int Count)
{
    /// <summary>Whether this is a status item (loading or failed) rather than a node.</summary>
    public bool IsStatus => Node is null;
}
