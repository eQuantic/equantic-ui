namespace eQuantic.UI.Primitives;

/// <summary>
/// Draws an overlay WHERE IT STANDS, instead of over everything.
/// <para>
/// A dialog, a drawer, a sheet and a popover only exist once something opens them, and when they do
/// they take the whole viewport: a scrim over the page, the surface centred or pinned in front of
/// it, an Escape binding, an enter animation. A page that wants to SHOW one — documentation, a
/// design review, a visual-regression suite — wants the surface and none of that machinery, and had
/// no way to ask for it. Five components rendered blank.
/// </para>
/// <para>
/// The component decides what its surface is, because the component is the only thing that knows.
/// This node states the intent; each overlay answers it by building its panel and nothing else.
/// </para>
/// <para>
/// It is not a preview mode: the surface is a real one, with real actions. What is dropped is the
/// LAYER — the scrim, the viewport sizing, the portal and the keyboard binding that only make sense
/// for something floating above a page.
/// </para>
/// <para>
/// A component reads it from its context, which answers from the tree: whether an <see cref="InFlow"/>
/// stands above its position, or, for a tree drawn without positions, the flag the realizer hands the
/// context it builds the subtree with (#1395).
/// </para>
/// </summary>
public sealed class InFlow : SingleChildNode
{
    public override string NodeKind => "inFlow";

    public InFlow(VisualNode child) : base(child) { }


    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
