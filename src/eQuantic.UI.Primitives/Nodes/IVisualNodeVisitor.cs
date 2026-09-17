namespace eQuantic.UI.Primitives;

/// <summary>
/// One method per node, so a pass over the vocabulary is TOLD when the vocabulary grows.
///
/// <para>
/// Six dispatches walk these nodes — layout, three realizers, semantics, and the runtime's own
/// lowering — and every one of them was a switch with a default arm. A node added to the vocabulary
/// joined that arm silently in all six, which is how a spreadsheet rendered as an empty
/// <c>&lt;span&gt;</c> to every crawler for a month: nothing asked the web realizer whether it had
/// heard of <c>SheetSurface</c>. The coverage pin that found it reads the switches with a regex,
/// which is a net stretched under a hole rather than the hole closed.
/// </para>
///
/// <para>
/// FIVE HAVE CROSSED: the semantics walk (<c>SemanticsVisitor</c>), both email alternatives over one
/// shared refusal set (<c>EmailWalk</c>), the web realizer (<c>WebLoweringVisitor</c>), the layout
/// engine (<c>MeasureVisitor</c>) and the runtime's lowering — that last one differently, through a
/// generated <c>NodeKind</c> union and an <c>assertNever</c>, since the browser has no interface to
/// implement. ONE TO GO: <c>PhotonRealizer.EmitNode</c>.
/// </para>
///
/// <para>
/// That sentence is a running tally, which is the shape that rots — it was written at four and was
/// still saying four two slices later, in the doc of the very interface the crossings implement.
/// It is the LAST one: when the Photon realizer crosses, this says all six and stops being a
/// countdown. Until then the slice table in <c>docs/VOCABULARY-DISPATCH-PLAN.md</c> is where the
/// detail lives, and <c>docs/ARCHITECTURE-AUDIT.md</c> section 2 carries the same status in one
/// line.
/// </para>
///
/// <para>
/// This is the hole closed, and it is Flutter's answer transposed: there, a node IS its own
/// behaviour (<c>performLayout</c>, <c>paint</c>), so the compiler asks the question at the
/// declaration. We cannot put six realizers' worth of behaviour on the node — a realizer lives in
/// another assembly and must not be referenced from the vocabulary — so the question moves to the
/// visitor instead. Adding a node adds a method here, and every implementation stops compiling
/// until it has an answer.
/// </para>
///
/// <para>
/// <typeparamref name="TState"/> travels DOWN and <typeparamref name="TResult"/> comes back up,
/// which is what lets one interface serve passes that are shaped very differently: a realizer
/// carries a style context down and hands a lowered tree up, a semantics walk carries a path down
/// and appends to a list. A pass that wants neither says <see cref="Nothing"/>.
/// </para>
///
/// <para>
/// <see cref="UiComponent"/> is here as ONE method, not as its subclasses: an app's components are
/// unbounded and the vocabulary is not, so a visitor answers "a component" once and asks it to
/// build. That is also why this interface can be exhaustive at all.
/// </para>
/// </summary>
public interface IVisualNodeVisitor<in TState, out TResult>
{
    /// <summary>An app's own component — the one node kind this interface cannot enumerate, and the
    /// reason it stays closed everywhere else.</summary>
    TResult Visit(UiComponent node, TState state);

    TResult Visit(AdaptiveNode node, TState state);

    TResult Visit(Adjustable node, TState state);

    TResult Visit(Anchored node, TState state);

    TResult Visit(Box node, TState state);

    TResult Visit(CameraPreview node, TState state);

    TResult Visit(Canvas node, TState state);

    TResult Visit(CodeSurface node, TState state);

    TResult Visit(Column node, TState state);

    TResult Visit(DragDismiss node, TState state);

    TResult Visit(Draggable node, TState state);

    TResult Visit(Drawing node, TState state);

    TResult Visit(Flexible node, TState state);

    TResult Visit(Grid node, TState state);

    TResult Visit(Hoverable node, TState state);

    TResult Visit(Icon node, TState state);

    TResult Visit(Image node, TState state);

    TResult Visit(InFlow node, TState state);

    TResult Visit(InView node, TState state);

    TResult Visit(Link node, TState state);

    TResult Visit(LoopMotion node, TState state);

    TResult Visit(Navigable node, TState state);

    TResult Visit(Overlay node, TState state);

    TResult Visit(Pinned node, TState state);

    TResult Visit(Positioned node, TState state);

    TResult Visit(Presence node, TState state);

    TResult Visit(Pressable node, TState state);

    TResult Visit(Row node, TState state);

    TResult Visit(SafeArea node, TState state);

    TResult Visit(ScrollView node, TState state);

    TResult Visit(SheetSurface node, TState state);

    TResult Visit(Shortcut node, TState state);

    TResult Visit(Simulated node, TState state);

    TResult Visit(Spacer node, TState state);

    TResult Visit(Spinner node, TState state);

    TResult Visit(Stack node, TState state);

    TResult Visit(Text node, TState state);

    TResult Visit(TextEntry node, TState state);

    TResult Visit(Vector node, TState state);

    TResult Visit(WebFrame node, TState state);
}
