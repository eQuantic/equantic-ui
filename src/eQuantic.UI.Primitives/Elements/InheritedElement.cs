namespace eQuantic.UI.Primitives;

/// <summary>
/// The position of an <see cref="InheritedComponent"/>: it holds the node's one child, and the
/// component positions below it that read it through <c>DependOn</c>. Flutter's
/// <c>InheritedElement</c> (docs/ELEMENT-TREE-PLAN.md, 3.4).
/// <para>
/// When a parent's build hands this position a new node whose <see cref="InheritedComponent.UpdateShouldNotify"/>
/// answers true, every dependent is marked and builds in the same frame, and nothing else does: the
/// subtree between this position and its dependents keeps what it built, unless its own nodes changed.
/// </para>
/// </summary>
public sealed class InheritedElement : Element
{
    private Element? _child;
    private readonly HashSet<ComponentElement> _dependents = new();

    internal InheritedElement(InheritedComponent inherited) : base(inherited)
    {
    }

    /// <summary>The node this position holds now.</summary>
    public InheritedComponent Inherited => (InheritedComponent)Configuration;

    /// <summary>The inherited position above this one: the next one a lookup asks when this one is not
    /// what it looks for.</summary>
    internal InheritedElement? Above => NearestInherited;

    /// <inheritdoc />
    public override Element? ChildAt(int slot) => slot == 0 ? _child : null;

    /// <inheritdoc />
    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }

    private protected override void FirstBuild()
    {
        _child = UpdateChild(null, Inherited.Child, force: false);
    }

    internal override void Update(VisualNode configuration, bool force)
    {
        if (ReferenceEquals(configuration, Configuration))
        {
            if (!force) return;
        }
        else
        {
            var old = Inherited;
            Configuration = configuration;
            if (Inherited.UpdateShouldNotify(old)) NotifyDependents();
        }

        _child = UpdateChild(_child, Inherited.Child, force);
    }

    /// <summary>A component position that read this one, and builds again when it changes.</summary>
    internal void AddDependent(ComponentElement dependent)
    {
        _dependents.Add(dependent);
    }

    /// <summary>A dependent that left the tree.</summary>
    internal void RemoveDependent(ComponentElement dependent)
    {
        _dependents.Remove(dependent);
    }

    private void NotifyDependents()
    {
        foreach (var dependent in _dependents) dependent.DependencyChanged();
    }

    private protected override void DeactivateChildren()
    {
        if (_child is null) return;
        _child.Deactivate();
        _child = null;
    }

    private protected override void OnDeactivated()
    {
        _dependents.Clear();
    }
}
