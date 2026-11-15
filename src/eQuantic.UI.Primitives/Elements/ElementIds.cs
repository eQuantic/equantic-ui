namespace eQuantic.UI.Primitives;

/// <summary>
/// The count element trees take their ids from (<see cref="Element.Id"/>,
/// <see cref="NodeElement.PartId"/>), counted from one, so zero is never an id, and never giving one
/// twice. An owner makes a count of its own unless it is handed one: a host that keeps several trees
/// and names their nodes in ONE space hands each the same count. The browser does, with one count for
/// the document, because a page owns a tree per root it lowers and the attributes that find a node
/// after a render carry its element's id (docs/ELEMENT-TREE-PLAN.md, 3.7): two roots counting alone
/// would give two nodes the same one.
/// <para>
/// <em>How does Flutter solve it?</em> A semantics node's id comes from one count for the whole
/// process, whichever tree the node is in.
/// </para>
/// </summary>
public sealed class ElementIds
{
    private int _last;

    /// <summary>The next id, which this count has never given.</summary>
    public int Next() => ++_last;
}
