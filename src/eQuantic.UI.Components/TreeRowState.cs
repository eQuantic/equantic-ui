namespace eQuantic.UI.Components;

/// <summary>
/// What a <see cref="TreeView"/>'s <see cref="TreeView.RowBuilder"/> builds a node's row from (#1532): the
/// <see cref="Node"/>, its <see cref="Level"/> from one, whether it is open (<see cref="Expanded"/>, null for a node
/// with nothing to open), whether it is the app's current node (<see cref="Selected"/>) and whether it is
/// <see cref="Busy"/>. The tree draws the row around what the builder returns, so it says nothing of where the row
/// is drawn.
/// <para>
/// <em>How does Flutter solve it?</em> <c>TreeView.treeNodeBuilder</c> receives the <c>TreeViewNode</c>, which
/// carries its depth and whether it is expanded.
/// </para>
/// </summary>
public sealed record TreeRowState(TreeNode Node, int Level, bool? Expanded, bool Selected, bool Busy);
