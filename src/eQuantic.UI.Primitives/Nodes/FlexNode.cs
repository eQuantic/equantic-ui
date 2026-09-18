using System.Collections;

namespace eQuantic.UI.Primitives;

/// <summary>
/// The only flow layout (spec A2): a single main axis, token Gap between children (the only legal
/// sibling spacing — it never collapses), <see cref="Flexible"/> children sharing leftover space by
/// weight. Truncation contract: TEXT children shrink to ellipsis before any sibling is pushed out;
/// fixed children (icons, avatars) never shrink.
/// </summary>
/// <remarks>
/// HOST ONLY, for the reason <see cref="SingleChildNode"/> carries the same attribute, and this one
/// waited a release longer on purpose. Every public `Primitives` type silently promises a runtime
/// export of the same name, because the transpiler routes that namespace to `@equantic/runtime` — so
/// a component naming this one in a property, a signature or a type expression emits an import of an
/// export that is not there and dies at HYDRATION, while SSR keeps answering 200 with correct
/// markup. That is how `RouteValues` shipped once.
/// <para>
/// It sat in the runtime pin's `NO_TWIN_OWED` list instead, which is that list's WEAKER side — an
/// excuse rather than a rule — and #226 left it there deliberately: fencing a type that has already
/// shipped newly refuses code that compiles today, which is a decision with a blast radius rather
/// than a drive-by on a refactor. The radius was then MEASURED (#228). Nothing in `samples`,
/// `Components`, `Charts` or `Templates` names `FlexNode`; every reference is the framework's own
/// machinery — the two nodes that derive from it, the three visitors that pattern-match it, and one
/// generic constraint.
/// </para>
/// <para>
/// That constraint is the surface worth knowing about:
/// <c>VisualNodeExtensions.With&lt;T&gt;(this T, VisualNode) where T : FlexNode</c> is public, and a
/// page writing <c>Row(...).With(child)</c> reaches it. It still compiles, and the fence is why:
/// since #226 it asks what type a member was reached THROUGH, and <c>With</c> is declared on
/// <c>VisualNodeExtensions</c> and reached through <c>Row</c>. Neither is host-only. A page that
/// names the SHAPE is refused; a page that uses a row is not.
/// </para>
/// </remarks>
[ServerOnly]
public abstract class FlexNode : VisualNode, IEnumerable<VisualNode>
{
    /// <summary>A public abstract intermediate INHERITS an accessible constructor, so closing
    /// <see cref="VisualNode"/> alone left this door open — a second `FlexNode` could be declared
    /// anywhere. Found in review of the closure, not by reading the base class.</summary>
    private protected FlexNode() { }

    private readonly List<VisualNode> _children = new();

    public float Gap { get; init; }

    /// <summary>
    /// Spec S3 flow wrapping (the CSS <c>flex-wrap: wrap</c> twin): children that overflow the main
    /// extent break onto the next line. v1 scope: children keep their NATURAL main size — Flexible
    /// weights don't distribute inside a wrapping container (use a non-wrapping Row for that).
    /// </summary>
    public bool Wrap { get; init; }

    /// <summary>Spacing BETWEEN WRAPPED LINES (spec S3). <c>null</c> = same as <see cref="Gap"/>.</summary>
    public float? RunGap { get; init; }

    public MainAlign Main { get; init; } = MainAlign.Start;
    public abstract CrossAlign Cross { get; init; }
    public EdgeInsets Padding { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }
    /// <summary>Optional container background (sugar for wrapping in a Box).</summary>
    public ColorToken? Background { get; init; }
    public CornerRadii CornerRadius { get; init; }

    public IReadOnlyList<VisualNode> Children => _children;

    /// <summary>Collection-initializer support: <c>new Column(gap: Space.S4) { a, b, c }</c>.</summary>
    public void Add(VisualNode child) => _children.Add(child);

    public IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();
}
