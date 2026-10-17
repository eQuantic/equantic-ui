namespace eQuantic.UI.Primitives;

/// <summary>
/// The position of a vocabulary node: it builds nothing, and holds its children by slot
/// (<see cref="ChildSlots"/>).
/// <para>
/// Which old child a new one is compared with is what the layout's paths always said, so no
/// component's retention moves: a fixed slot compares its one occupant, and the members of a list
/// (a <see cref="Row"/>, a <see cref="Column"/>, a <see cref="Stack"/>, a <see cref="Grid"/>, a
/// <see cref="Navigable"/>'s rows) are matched by key when they have one, wherever it moved, and by
/// index when they do not.
/// </para>
/// </summary>
[ServerOnly]
public sealed class NodeElement : Element
{
    private List<Element?> _children = new();
    private List<Element?>? _spare;

    /// <summary>The slot the realizer shows (an overlay's child, an anchored panel's layer), or -1.</summary>
    private int _deferred = -1;

    /// <summary>The frame the deferred slot was last shown in: one that was not shown in a frame is
    /// taken out when that frame ends, which unmounts a closed layer's components.</summary>
    private int _shownInFrame = -1;

    internal NodeElement(VisualNode configuration) : base(configuration)
    {
    }

    /// <inheritdoc />
    public override Element? ChildAt(int slot) => (uint)slot < (uint)_children.Count ? _children[slot] : null;

    /// <summary>
    /// Builds the deferred slot with what the realizer is about to show in it, and answers its
    /// position: the realizer lays the layer out against it. Called once per frame the layer is
    /// shown; a frame that does not call it takes the slot's subtree out when it ends.
    /// </summary>
    public Element ShowDeferred(VisualNode configuration)
    {
        if (_deferred < 0)
            throw new InvalidOperationException(
                $"A {Configuration.GetType().Name} has no slot the realizer shows.");

        Owner.EnterBuild();
        try
        {
            var shown = UpdateChild(ChildAt(_deferred), configuration, Owner.RebuildingAll)!;
            _children[_deferred] = shown;
            _shownInFrame = Owner.Frame;
            // What a SetState marked under the slot builds now, with the configuration the layer
            // shows; the dirty list left it for this moment (BuildOwner.BuildDeferred).
            Owner.BuildDeferred(this);
            return shown;
        }
        finally
        {
            Owner.ExitBuild();
        }
    }

    private protected override void FirstBuild()
    {
        UpdateChildren(force: false);
        if (_deferred >= 0) Owner.TrackDeferred(this);
    }

    internal override void Update(VisualNode configuration, bool force)
    {
        if (ReferenceEquals(configuration, Configuration) && !force) return;
        Configuration = configuration;
        UpdateChildren(force);
    }

    /// <summary>Whether <paramref name="child"/> is the position in this node's deferred slot.</summary>
    internal bool HoldsDeferred(Element child) => _deferred >= 0 && ReferenceEquals(ChildAt(_deferred), child);

    /// <summary>Takes the deferred slot's subtree out when the frame that ends did not show it.</summary>
    internal void SweepDeferred(int frame)
    {
        if (_deferred < 0 || _shownInFrame == frame || ChildAt(_deferred) is not { } hidden) return;
        Owner.Deactivate(hidden);
        _children[_deferred] = null;
    }

    private void UpdateChildren(bool force)
    {
        var slots = ChildSlots.Of(Configuration, Owner.SizeClass);
        _deferred = slots.Deferred;
        if (slots.IsList && AnyKey(slots))
        {
            UpdateKeyed(slots, force);
            return;
        }

        var count = slots.Count;
        while (_children.Count < count) _children.Add(null);
        for (var slot = 0; slot < count; slot++)
        {
            // Left as it is: the realizer builds it when it shows it (see ShowDeferred).
            if (slot == _deferred) continue;
            _children[slot] = UpdateChild(_children[slot], slots[slot], force);
        }

        for (var slot = _children.Count - 1; slot >= count; slot--)
        {
            if (_children[slot] is { } gone) Owner.Deactivate(gone);
            _children.RemoveAt(slot);
        }
    }

    /// <summary>Whether a key is in play, on either side: only then can a member have moved.</summary>
    private bool AnyKey(NodeSlots slots)
    {
        for (var i = 0; i < slots.Count; i++)
            if (slots[i]?.Key is { Length: > 0 }) return true;
        for (var i = 0; i < _children.Count; i++)
            if (_children[i]?.Configuration.Key is { Length: > 0 }) return true;
        return false;
    }

    /// <summary>
    /// A list in which a key is in play. The old members are claimed first (a keyed one by its key, an
    /// unkeyed one by its index, either only by a node of its own type), what nobody claimed leaves,
    /// and only then does each member build, so the claiming never runs inside a child's build and the
    /// owner's one scratch table is enough for the whole tree.
    /// </summary>
    private void UpdateKeyed(NodeSlots slots, bool force)
    {
        var stamp = Owner.NextStamp();
        var byKey = Owner.KeyScratch;
        byKey.Clear();
        foreach (var old in _children)
        {
            if (old?.Configuration.Key is { Length: > 0 } key) byKey.TryAdd(key, old);
        }

        var next = _spare ??= new List<Element?>();
        next.Clear();
        var count = slots.Count;
        for (var i = 0; i < count; i++)
        {
            var node = slots[i]!;
            Element? claimed = null;
            if (node.Key is { Length: > 0 } key)
            {
                claimed = byKey.TryGetValue(key, out var keyed) && keyed.MatchStamp != stamp
                                                                && CanUpdate(keyed.Configuration, node)
                    ? keyed
                    : NextUnclaimed(node, stamp);
            }
            else if (i < _children.Count && _children[i] is { } indexed && indexed.Configuration.Key is not { Length: > 0 }
                     && indexed.MatchStamp != stamp && CanUpdate(indexed.Configuration, node))
            {
                claimed = indexed;
            }

            if (claimed is not null) claimed.MatchStamp = stamp;
            next.Add(claimed);
        }

        byKey.Clear();
        foreach (var old in _children)
        {
            if (old is not null && old.MatchStamp != stamp) Owner.Deactivate(old);
        }

        (_children, _spare) = (next, _children);
        _spare.Clear();
        for (var i = 0; i < count; i++)
        {
            var node = slots[i]!;
            if (_children[i] is { } kept) kept.Update(node, force);
            else _children[i] = Inflate(node);
        }
    }

    /// <summary>
    /// The first old member <paramref name="node"/> can take that nothing claimed yet, in order: what a
    /// key shared by two siblings falls back to, since the table holds one member per key. Siblings
    /// that repeat a key are matched first to first and second to second, so each keeps its state
    /// across a rebuild instead of the second being made again every time. Only reached when the
    /// table's member was taken or is of another type, which a list without repeated keys never is.
    /// </summary>
    private Element? NextUnclaimed(VisualNode node, int stamp)
    {
        foreach (var old in _children)
        {
            if (old is not null && old.MatchStamp != stamp && CanUpdate(old.Configuration, node)) return old;
        }

        return null;
    }

    private protected override void DeactivateChildren()
    {
        for (var i = 0; i < _children.Count; i++)
        {
            _children[i]?.Deactivate();
            _children[i] = null;
        }
    }
}
