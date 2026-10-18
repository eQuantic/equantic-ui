namespace eQuantic.UI.Primitives;

/// <summary>
/// A POSITION in a live tree: the node it holds now, its parent, and the children it keeps by slot.
/// <para>
/// Flutter's middle tree (docs/ELEMENT-TREE-PLAN.md, 3.1). A <see cref="VisualNode"/> is rebuilt
/// constantly and is only configuration; the element is what stays while a rebuild hands it a new
/// node of the same runtime type and key, and what goes when the node at its position changes kind.
/// Whatever belongs to a position (a stateful component's instance, and in E2 a scroll offset, a
/// presence, a drag) is held here, so it dies with the position instead of being inherited by the
/// next node a frame happens to address the same way.
/// </para>
/// <para>
/// Two kinds: <see cref="ComponentElement"/>, which builds, and <see cref="NodeElement"/>, which holds
/// a vocabulary node's children. The tree is driven by a <see cref="BuildOwner"/>; nothing outside
/// the engine makes one.
/// </para>
/// <para>
/// HOST ONLY until the browser drives the same engine (E3): eqc routes this namespace to the runtime,
/// so a page naming it would compile and die at hydration, and the attribute moves that to the build.
/// </para>
/// </summary>
[ServerOnly]
public abstract class Element
{
    private protected Element(VisualNode configuration)
    {
        Configuration = configuration;
    }

    /// <summary>The node this position holds now: the last one a build handed it.</summary>
    public VisualNode Configuration { get; private protected set; }

    /// <summary>The position this one is a child of, or null at the root.</summary>
    public Element? Parent { get; private set; }

    /// <summary>How many positions are above this one; the root's is zero. Dirty components build
    /// in this order, so a parent hands its children their configuration before they build.</summary>
    public int Depth { get; private set; }

    /// <summary>Whether this position is in its tree: mounted, and not yet taken out of it.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// The id this position carries from the moment it mounts for as long as it lives, unique in its
    /// tree and never given to another: what a host and a platform's accessibility bridge name a node
    /// by. A node that takes another's place mounts a new position, and so has a new id. Flutter's
    /// <c>SemanticsNode.id</c> is the same kind of number.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>The position at <paramref name="slot"/>, or null when that slot holds nothing.</summary>
    public abstract Element? ChildAt(int slot);

    /// <summary>The tree this position belongs to.</summary>
    internal BuildOwner Owner { get; private set; } = null!;

    /// <summary>How many COMPONENT positions are above this one: the nesting the error boundary
    /// bounds, read from the tree instead of counted by a walk.</summary>
    internal int ComponentDepth { get; private set; }

    /// <summary>Whether an <see cref="Primitives.InFlow"/> is above this position, so a component
    /// that builds alone, outside its parent's walk, still builds with the intent armed.</summary>
    internal bool UnderInFlow { get; private set; }

    /// <summary>The reconciliation that last claimed this position (see <see cref="NodeElement"/>):
    /// a stamp rather than a flag, so nothing has to be cleared between two of them.</summary>
    internal int MatchStamp;

    /// <summary>The element a node is held by: a component builds, every other node holds children.</summary>
    internal static Element Create(VisualNode configuration) => configuration is UiComponent component
        ? new ComponentElement(component)
        : new NodeElement(configuration);

    /// <summary>
    /// Flutter's <c>canUpdate</c>: a position keeps its element when the next node is of the same
    /// runtime type and carries the same key, and is replaced otherwise.
    /// </summary>
    internal static bool CanUpdate(VisualNode current, VisualNode next) =>
        current.GetType() == next.GetType() && string.Equals(current.Key, next.Key, StringComparison.Ordinal);

    /// <summary>Takes its place under <paramref name="parent"/> and builds what is below it.</summary>
    internal void Mount(BuildOwner owner, Element? parent)
    {
        Owner = owner;
        Parent = parent;
        Id = owner.NextId();
        Depth = parent is null ? 0 : parent.Depth + 1;
        ComponentDepth = parent is null ? 0 : parent.ComponentDepth + (parent is ComponentElement ? 1 : 0);
        UnderInFlow = parent is not null && (parent.UnderInFlow || parent.Configuration is InFlow);
        IsActive = true;
        FirstBuild();
    }

    /// <summary>What mounting means for this kind: a component builds, a node mounts its children.</summary>
    private protected abstract void FirstBuild();

    /// <summary>
    /// Takes a new node at this position. A node that is the SAME instance as the one held changes
    /// nothing, unless the frame rebuilds everything (<paramref name="force"/>) or the position is
    /// dirty.
    /// </summary>
    internal abstract void Update(VisualNode configuration, bool force);

    /// <summary>
    /// The child at a slot after a build: <paramref name="child"/> kept and updated when it can take
    /// <paramref name="configuration"/>, replaced when it cannot, and taken out when there is no
    /// node for the slot any more.
    /// </summary>
    private protected Element? UpdateChild(Element? child, VisualNode? configuration, bool force)
    {
        if (configuration is null)
        {
            if (child is not null) Owner.Deactivate(child);
            return null;
        }

        if (child is not null)
        {
            if (CanUpdate(child.Configuration, configuration))
            {
                child.Update(configuration, force);
                return child;
            }

            Owner.Deactivate(child);
        }

        return Inflate(configuration);
    }

    /// <summary>A new position under this one, mounted and built.</summary>
    private protected Element Inflate(VisualNode configuration)
    {
        var created = Create(configuration);
        created.Mount(Owner, this);
        return created;
    }

    /// <summary>
    /// Takes this position and everything below it out of the tree, the children first: Flutter's
    /// order, so a component lets go of what it holds before the one that built it does.
    /// </summary>
    internal void Deactivate()
    {
        DeactivateChildren();
        IsActive = false;
        OnDeactivated();
    }

    private protected abstract void DeactivateChildren();

    /// <summary>What leaving means for this kind beyond its children (a component's unmount).</summary>
    private protected virtual void OnDeactivated()
    {
    }
}
