namespace eQuantic.UI.Components;

/// <summary>
/// Where a <see cref="TreeView"/> node was dropped (#1533): the <see cref="Node"/> moved, its new
/// <see cref="Parent"/> (null at the top level) and the sibling it lands <see cref="After"/> (null when it lands
/// first, as a node dropped inside another does). The move is the app's to make, as a sortable's drop is.
/// </summary>
public sealed record TreeMove(TreeNode Node, TreeNode? Parent, TreeNode? After);
