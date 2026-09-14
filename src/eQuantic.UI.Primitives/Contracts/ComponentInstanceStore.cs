namespace eQuantic.UI.Primitives;

/// <summary>
/// The positional reconciler's instance store (plan W6, slice 1): retains STATEFUL component
/// instances across build passes by identity — tree PATH + runtime TYPE + optional
/// <see cref="VisualNode.Key"/>. A parent's Build() constructs fresh instances every pass; when the
/// store holds a retained instance with the same identity, the retained one (its state alive) is
/// used instead, ADOPTING the fresh instance's configuration via
/// <see cref="UiComponent.AdoptConfig"/>. Identity mismatches (type or key changed) discard the
/// retained instance — state resets, matching every positional reconciler's contract. Stateless
/// components pass through untouched (they have no state to retain).
/// </summary>
public sealed class ComponentInstanceStore
{
    private Dictionary<string, StatefulComponent> _retained = new();
    private Dictionary<string, StatefulComponent> _visited = new();

    /// <summary>Fires once when an instance ENTERS retention — the host wires its invalidation there.</summary>
    public event Action<StatefulComponent>? InstanceRetained;

    // Mounts are QUEUED here and delivered when the pass closes, never mid-build. A hook that ran
    // inside Reconcile would run while its own parent is still building, so a SetState from it would
    // mutate a tree half-way through being produced.
    private readonly List<StatefulComponent> _mounting = new();

    /// <summary>
    /// Resolves the instance to use for <paramref name="fresh"/> at <paramref name="path"/>: the
    /// retained instance when the identity matches (after adopting the fresh config), otherwise the
    /// fresh instance (which starts its retention).
    /// </summary>
    public UiComponent Reconcile(string path, UiComponent fresh)
    {
        if (fresh is not StatefulComponent freshStateful) return fresh;

        var identity = $"{path}#{fresh.GetType().FullName}#{fresh.Key}";
        if (_retained.TryGetValue(identity, out var retained) && !ReferenceEquals(retained, freshStateful))
        {
            retained.AdoptConfig(freshStateful);
            _visited[identity] = retained;
            return retained;
        }

        _visited[identity] = freshStateful;
        InstanceRetained?.Invoke(freshStateful);
        _mounting.Add(freshStateful);
        return freshStateful;
    }

    /// <summary>
    /// Ends a build pass: entries not visited this pass are DROPPED — their position left the tree,
    /// which is exactly when <see cref="StatefulComponent.OnUnmount"/> is owed. The instances that
    /// ENTERED this pass are mounted here too, once the tree they belong to is finished.
    /// Call once per completed pass.
    /// </summary>
    public void EndPass()
    {
        (_retained, _visited) = (_visited, _retained);

        // _visited now holds the PREVIOUS pass: anything in it the new pass did not keep is gone.
        foreach (var (identity, instance) in _visited)
        {
            if (!_retained.TryGetValue(identity, out var kept) || !ReferenceEquals(kept, instance))
                instance.NotifyUnmounted();
        }

        _visited.Clear();

        // Mounted LAST, so a component's OnMount sees a tree in which everything that left has
        // already unsubscribed — the order that lets two components share one exclusive resource
        // across a swap.
        foreach (var instance in _mounting)
        {
            instance.NotifyMounted();
        }

        _mounting.Clear();
    }

    /// <summary>
    /// Tears the whole store down — every retained instance leaves the tree at once. What a surface
    /// calls when it is closing, so the last tree unsubscribes instead of being collected silently.
    /// </summary>
    public void UnmountAll()
    {
        foreach (var instance in _retained.Values) instance.NotifyUnmounted();
        _retained.Clear();
        _visited.Clear();
        _mounting.Clear();
    }
}
