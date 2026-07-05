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
        return freshStateful;
    }

    /// <summary>
    /// Ends a build pass: entries not visited this pass are DROPPED (their position left the tree —
    /// state disposal semantics arrive with lifecycle hooks). Call once per completed pass.
    /// </summary>
    public void EndPass()
    {
        (_retained, _visited) = (_visited, _retained);
        _visited.Clear();
    }
}
