namespace eQuantic.UI.Primitives;

/// <summary>
/// Base of the ABSTRACT visual vocabulary (docs/SHARED-COMPONENTS-PLAN.md) — the closed set of nodes
/// shared components are authored against, written once and lowered per target by a realizer: the web
/// realizer to HtmlElement/DOM + CSS, the native realizer to Photon primitives. Nodes are immutable
/// build products (rebuilt each Build pass, diffed by the reconciler); they hold TOKENS, never
/// resolved colors, so one tree realizes in any theme mode.
/// </summary>
public abstract class VisualNode
{
    /// <summary>
    /// CLOSED BY CONSTRUCTION. The vocabulary is a fixed set, and <see cref="IVisualNodeVisitor{TState,TResult}"/>
    /// is only exhaustive while nothing outside this assembly can add to it — a public constructor
    /// here is an open door that no test can shut afterwards, because the subtype would live in
    /// somebody else's tree.
    /// <para>
    /// The ONE seam apps use is <see cref="UiComponent"/>, whose constructor stays <c>protected</c>
    /// on purpose: an app's components are unbounded, which is exactly why the visitor answers
    /// "a component" once and asks it to build.
    /// </para>
    /// </summary>
    private protected VisualNode() { }

    /// <summary>
    /// Hands this node to a visitor's method for its own type — the vocabulary's dispatch, and the
    /// only one the COMPILER checks. <typeparamref name="TState"/> travels down, <typeparamref name="TResult"/>
    /// comes back up, and a pass that wants neither says <see cref="Nothing"/>.
    /// </summary>
    /// <remarks>
    /// HOST ONLY. A component BUILDS a tree; walking one is what a realizer, a layout pass or a
    /// semantics walk does, and the runtime ships no `accept` on its own `VisualNode`. Without the
    /// fence a page calling this compiled and emitted `node.accept(...)` — measured — which throws
    /// in the browser on a method that is not there.
    /// <para>
    /// Declared once, on the abstract: the fence follows an OVERRIDE to what it overrides, so the
    /// 40 one-line implementations carry it without saying so and a forty-first cannot forget.
    /// </para>
    /// </remarks>
    [ServerOnly]
    public abstract TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state);

    /// <summary>Reconciler identity across rebuilds (keyed diffing) — same contract as the web SDK.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// A named place in the screen that a link can reach — <c>Link("#features", …)</c> arrives at
    /// the node that says <c>Bookmark = "features"</c>.
    /// </summary>
    /// <remarks>
    /// NOT called Anchor, though that is the word the web uses: <see cref="Anchored"/> already
    /// spends it on the node a popover is positioned against, and one vocabulary cannot hold two
    /// meanings for one word. Not <see cref="Key"/> either, which the two were confused for until a
    /// site's whole landing nav turned out to be dead links: a Key need only be unique among
    /// SIBLINGS, and in a list it is typically "1", "2", "3" — projecting that onto a screen-wide
    /// name would collide on the first repeated list. A Key answers "which node is this, across
    /// rebuilds"; a Bookmark answers "what may point at it". A node can have both, and a node whose
    /// Key is a real reconciliation key must not have that key leak into the document.
    /// <para>
    /// Nothing validates the value beyond emptiness, on purpose. These are frequently GENERATED —
    /// a documentation rail slugs the headings it finds — so the author often never types one, and
    /// refusing a duplicate at build time would fail a docs page because the source it renders grew
    /// a repeated heading. A collision is the page's to fix, not the SDK's to forbid.
    /// </para>
    /// <para>
    /// On the web this becomes the element's <c>id</c>, and the realizer also gives it room to
    /// land: a browser scrolls a target to the very top, so under a pinned header the target
    /// arrives HIDDEN. The offset comes from the pinned the app already declared, measured at run
    /// time, never a number the author repeats — one app here has a 60dp nav on one page and a
    /// 56dp topbar on another, and states neither.
    /// </para>
    /// <para>
    /// ON PHOTON it is inert TODAY, and that is a fence rather than an oversight: a bookmark is
    /// somewhere to scroll to, and the desktop track has no programmatic scrolling yet (Track W's
    /// W4 owns it). Declaring one there is not wrong and costs nothing — it is the same screen,
    /// written once — it simply has nothing to answer until there is something that scrolls.
    /// </para>
    /// <para>
    /// ONE PLACE WHERE THE COLLISION IS NOT THE PAGE'S FAULT: a subtree placed in two arms of an
    /// <see cref="AdaptiveNode"/>. The web realizer emits every declared arm and hides the ones the
    /// media query rejects, so ONE bookmark the author typed reaches the document TWICE — and a
    /// browser resolves a fragment to the first in DOCUMENT order, which is the compact arm, sitting
    /// at zero height under <c>display:none</c>. The link works, the console is clean, and the page
    /// does not move. It is the same shape as SVG gradient ids (see the note in the web realizer's
    /// gradient hoisting), except an id cannot be hoisted to one container the way a gradient can.
    /// The cure is compositional and is worth reaching for anyway: put the shared subtree in the
    /// tree ONCE and make only the varying part adaptive.
    /// </para>
    /// </remarks>
    public string? Bookmark { get; init; }

    /// <summary>
    /// WHERE IN THE SOURCE this node was constructed — <c>path|startLine:startCol|endLine:endCol</c>,
    /// zero-based — or <c>null</c>, which is what production always is.
    /// <para>
    /// Set only by a DESIGN-MODE compilation, for tools that have to answer "which C# made this
    /// pixel". A string and not a file/span type on purpose: this layer speaks no target's language,
    /// and both realizers can carry an opaque string to their own world (a <c>data-eq-origin</c>
    /// attribute on the web; nothing at all on Photon, where <c>LayoutNode.Source</c> already hands a
    /// hit-test back the node itself).
    /// </para>
    /// <para>
    /// It is EXACT rather than correlated, which is the whole point: a row built five hundred times
    /// inside a <c>foreach</c> carries the span of the one expression that built it — and that
    /// expression is the only editable thing anyway.
    /// </para>
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>
    /// What to CALL this node in a design tool — the component's type, and the local it was assigned
    /// to when it has one (<c>Column · content</c>). Set only by a design-mode compilation, beside
    /// <see cref="Origin"/>.
    /// <para>
    /// Carried rather than derived because it is wanted on HOVER: resolving a name from the span
    /// means asking the compiler, and asking on every pointer move is a request per frame for an
    /// answer that never changes.
    /// </para>
    /// </summary>
    public string? OriginLabel { get; init; }

    /// <summary>
    /// Stamps <see cref="Origin"/> on a node that is already built — the C# twin of the JS emitter's
    /// <c>$eq.origin(…)</c>, called only by code a DESIGN-MODE emission injected. Reflection because
    /// the property is init-only ON PURPOSE: authors must not write it, and a tool that rewrites the
    /// compilation is not an author.
    /// <para>
    /// First stamp wins. The innermost expression executes first, so a construction's own exact span
    /// is already on the node by the time the helper call wrapping an ENCLOSING expression — a
    /// factory, a helper method returning the same instance — sees it. Overwriting here would replace
    /// the precise span with the caller's.
    /// </para>
    /// </summary>
    public static class DesignOrigin
    {
        private static readonly System.Reflection.PropertyInfo OriginProperty =
            typeof(VisualNode).GetProperty(nameof(Origin))!;

        public static T Stamp<T>(T node, string origin) where T : VisualNode
        {
            if (node.Origin is null) OriginProperty.SetValue(node, origin);
            return node;
        }
    }

    /// <summary>
    /// Overrides the parent flex container's <see cref="FlexNode.Cross"/> for THIS child only
    /// (spec S1 — the CSS <c>align-self</c> twin). <c>null</c> = follow the container. Ignored
    /// outside a Row/Column.
    /// </summary>
    public CrossAlign? AlignSelf { get; init; }

    /// <summary>Spec S4: how many grid COLUMNS this child spans (clamped to the row's remainder,
    /// the CSS auto-flow behavior). 0/1 = one column. Ignored outside a <see cref="Grid"/>.</summary>
    public int GridSpan { get; init; }

    /// <summary>
    /// WIRE DISCRIMINATOR: the node's kind as a stable string ("box", "row", …). Realizers that receive
    /// nodes across a serialization/transpilation boundary (the TypeScript runtime lowering) dispatch on
    /// this instead of CLR types — class names don't survive bundling. Sealed per node type.
    /// </summary>
    public abstract string NodeKind { get; }
}
