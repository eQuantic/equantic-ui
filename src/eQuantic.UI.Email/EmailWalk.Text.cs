using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Text and the editable surfaces — four words, of which the medium has one. A message is read, not
/// typed into: an entry, a code surface and a sheet are all editing, and there is no script in an
/// inbox to edit with.
/// </summary>
internal abstract partial class EmailWalk
{
    /// <summary>The theme's ramp, inlined onto every text — the one word this medium is made of.</summary>
    public abstract Nothing Visit(Text node, Nothing state);

    public Nothing Visit(CodeSurface node, Nothing state) => Refuse(node);

    public Nothing Visit(SheetSurface node, Nothing state) => Refuse(node);

    public Nothing Visit(TextEntry node, Nothing state) => Refuse(node);
}
