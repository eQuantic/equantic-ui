using System.Collections;

namespace eQuantic.UI.Primitives;

/// <summary>
/// The only flow layout (spec A2): a single main axis, token Gap between children (the only legal
/// sibling spacing — it never collapses), <see cref="Flexible"/> children sharing leftover space by
/// weight. Truncation contract: TEXT children shrink to ellipsis before any sibling is pushed out;
/// fixed children (icons, avatars) never shrink.
/// </summary>
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
