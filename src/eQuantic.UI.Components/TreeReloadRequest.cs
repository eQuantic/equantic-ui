using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// What an app asks a <see cref="TreeView"/> to read again (#1532): the children of each of <see cref="Keys"/> that
/// the tree loaded through its loader, after the app changed them itself (a deletion from a node's menu). The old
/// children stay on screen until the new ones arrive. Children the app gives in <see cref="TreeNode.Children"/> are
/// shown as the app builds them, and need no request. Each <see cref="Version"/> is honoured once, as a
/// <see cref="ScrollRequest"/>'s is.
/// </summary>
public sealed record TreeReloadRequest(int Version, IReadOnlyList<string> Keys);
