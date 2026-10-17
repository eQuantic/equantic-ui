namespace eQuantic.UI.Primitives;

/// <summary>
/// A node's children as its element holds them: either a LIST, whose members are matched by key
/// when they have one and by index when they do not, or up to two FIXED slots, each matched where it
/// stands. At most one fixed slot may be DEFERRED: built when the realizer shows it rather than by
/// the build phase (an overlay's child, an anchored panel's layer).
/// <para>
/// A struct over the node's own list, because every node of every rebuilt tree is asked: a view
/// allocates nothing.
/// </para>
/// </summary>
internal readonly struct NodeSlots
{
    private readonly IReadOnlyList<VisualNode>? _list;
    private readonly VisualNode? _first;
    private readonly VisualNode? _second;
    private readonly int _fixed;
    private readonly int _deferred;

    private NodeSlots(IReadOnlyList<VisualNode>? list, int fixedCount, VisualNode? first, VisualNode? second,
        int deferred)
    {
        _list = list;
        _fixed = fixedCount;
        _first = first;
        _second = second;
        // Stored one above the slot, so the default value of the struct means "none".
        _deferred = deferred + 1;
    }

    /// <summary>No child at all: the leaves.</summary>
    internal static NodeSlots None => default;

    /// <summary>Children matched by key or by index: a line, a stack, a grid, a navigable's rows.</summary>
    internal static NodeSlots Listed(IReadOnlyList<VisualNode> children) => new(children, 0, null, null, -1);

    /// <summary>One slot: a wrapper's child, a box's, an adaptive node's arm.</summary>
    internal static NodeSlots One(VisualNode? child) => new(null, 1, child, null, -1);

    /// <summary>Two slots, the second one possibly empty.</summary>
    internal static NodeSlots Two(VisualNode first, VisualNode? second) => new(null, 2, first, second, -1);

    /// <summary>Fixed slots of which <paramref name="slot"/> is built when the realizer shows it.</summary>
    internal static NodeSlots WithDeferred(NodeSlots slots, int slot) =>
        new(null, slots._fixed, slots._first, slots._second, slot);

    /// <summary>Whether the children are a list, matched by key where they have one.</summary>
    internal bool IsList => _list is not null;

    internal int Count => _list?.Count ?? _fixed;

    /// <summary>The deferred slot, or -1 when the node has none.</summary>
    internal int Deferred => _deferred - 1;

    internal VisualNode? this[int slot] => _list is { } list ? list[slot] : slot == 0 ? _first : _second;
}
