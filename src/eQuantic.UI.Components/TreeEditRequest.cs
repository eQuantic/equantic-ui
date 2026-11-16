using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// What an app asks a <see cref="TreeView"/> to edit (#1532): the node of <see cref="Key"/> renamed in place, or,
/// with <see cref="Creates"/>, a new item's field under it, drawn with <see cref="Icon"/>. A tree is built again
/// and again, so the request carries a <see cref="Version"/>, and the tree honours each version once: a request
/// still in the tree after the person moved on starts nothing, and a new version starts an edit again.
/// <para>
/// <em>How does Flutter solve it?</em> A controller the app calls. Here the asking is data in the tree, as a
/// <see cref="ScrollRequest"/>'s is, and the version is what makes data an event.
/// </para>
/// </summary>
public sealed record TreeEditRequest(int Version, string Key)
{
    /// <summary>Whether a new item is created under <see cref="Key"/>, rather than <see cref="Key"/> renamed.</summary>
    public bool Creates { get; init; }

    /// <summary>The new item's icon, a file's or a folder's; none when null. Read only when <see cref="Creates"/>.</summary>
    public IconGlyph? Icon { get; init; }
}
