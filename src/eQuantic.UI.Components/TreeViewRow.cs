namespace eQuantic.UI.Components;

/// <summary>
/// One VISIBLE row of a <see cref="TreeView"/>, in the order the tree draws them: a node, the status
/// item that stands under a node while its children load or after the load failed, or the field of a new
/// item being created under a node.
/// <para>
/// <see cref="Key"/> is the node's key, or for a status item or a new item's field the key of the node it
/// stands under;
/// <see cref="Level"/>, <see cref="Position"/> and <see cref="Count"/> are the item's place, from one,
/// as <c>TreePlace</c> states it.
/// </para>
/// </summary>
internal sealed record TreeViewRow(string Key, TreeNode? Node, string? ParentKey, int Level, int Position, int Count)
{
    /// <summary>Whether this is the field of a new item being created under <see cref="Key"/>.</summary>
    public bool IsField { get; init; }

    /// <summary>Whether this is a status item (loading or failed) rather than a node or a field.</summary>
    public bool IsStatus => Node is null && !IsField;
}
