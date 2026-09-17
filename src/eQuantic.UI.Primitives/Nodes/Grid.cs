using System.Collections;

namespace eQuantic.UI.Primitives;

/// <summary>
/// TRUE 2D layout (spec S4 — the CSS Grid twin, v1 auto-flow): explicit column tracks, children
/// placed left→right wrapping to new rows, per-child <see cref="VisualNode.GridSpan"/>. Rows size to
/// their tallest cell. Realized as CSS Grid on the web and a track-sizing pass on Photon.
/// </summary>
public sealed class Grid : VisualNode, IEnumerable<VisualNode>
{
    private readonly List<VisualNode> _children = new();

    public override string NodeKind => "grid";

    public Grid(IReadOnlyList<GridTrack> columns, float gap = 0, float? rowGap = null)
    {
        Columns = columns;
        Gap = gap;
        RowGap = rowGap;
    }

    public IReadOnlyList<GridTrack> Columns { get; }

    /// <summary>Gap between COLUMNS. <see cref="RowGap"/> defaults to it.</summary>
    public float Gap { get; init; }
    public float? RowGap { get; init; }
    public EdgeInsets Padding { get; init; }
    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    public IReadOnlyList<VisualNode> Children => _children;
    public void Add(VisualNode child) => _children.Add(child);
    public IEnumerator<VisualNode> GetEnumerator() => _children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _children.GetEnumerator();

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
