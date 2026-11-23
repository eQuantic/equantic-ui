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
/// Three kinds: <see cref="ComponentElement"/>, which builds, <see cref="InheritedElement"/>, which holds
/// a value for its subtree and knows what reads it, and <see cref="NodeElement"/>, which holds a
/// vocabulary node's children. The tree is driven by a <see cref="BuildOwner"/>; nothing outside the
/// engine makes one.
/// </para>
/// <para>
/// Written once (3.9): the engine transpiles into the runtime beside the code and spreadsheet engines,
/// so the browser can drive the very engine Photon drives, and a twin case holds the two to one answer.
/// </para>
/// </summary>
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

    /// <summary>
    /// Which of its parent's slots this position fills, Flutter's <c>slot</c>: the index its parent asks
    /// <see cref="ChildAt"/> with, zero at the root and under a component, and a keyed child's new index
    /// after a move. With its ancestors' it is where the position stands in the tree, read the same on
    /// every side that builds the same nodes, whatever order they built them in; what a host names a
    /// position by across a boundary (the server's data for the browser) is read from that chain.
    /// </summary>
    public int Slot { get; internal set; }

    /// <summary>
    /// Where this position stands below the positions <paramref name="depth"/> deep and shallower: the
    /// <see cref="Slot"/> of each position from there down to this one, joined by dots, and empty for a
    /// position at that depth. Every side that builds the same nodes reads the same path, whatever order
    /// it built them in and whichever arm of an adaptive node its window resolves, so a host names a
    /// position by it across a boundary: the server's data for the browser (#1477). A host whose root
    /// stands above the tree the other side builds from passes the root's depth below it.
    /// </summary>
    /// <param name="depth">The depth the path is measured from; zero measures it from the root.</param>
    public string SlotsBelow(int depth)
    {
        var slots = new List<int>();
        for (Element? at = this; at is not null && at.Depth > depth; at = at.Parent) slots.Add(at.Slot);
        slots.Reverse();
        return string.Join(".", slots);
    }

    /// <summary>The position at <paramref name="slot"/>, or null when that slot holds nothing.</summary>
    public abstract Element? ChildAt(int slot);

    /// <summary>
    /// Hands <paramref name="visitor"/> every child position this one keeps, in slot order, passing
    /// over a slot that holds nothing (a layer not shown yet): Flutter's <c>visitChildren</c>. The
    /// way a reader outside the engine walks a tree, because only the element knows how many slots
    /// it has; reading <see cref="ChildAt"/> until an empty one cannot tell an empty slot from the end.
    /// <para>
    /// ONE method by this name, and each kind overrides it and adds no other: the runtime twin the
    /// engine is transpiled into (E3) has one method per name, and the overloads of a visitor are what
    /// stopped it once (EQ1007). A plain loop and a delegate, for the same reason.
    /// </para>
    /// </summary>
    public abstract void VisitChildren(Action<Element> visitor);

    /// <summary>The tree this position belongs to.</summary>
    internal BuildOwner Owner { get; private set; } = null!;

    /// <summary>How many COMPONENT positions are above this one: the nesting the error boundary
    /// bounds, read from the tree instead of counted by a walk.</summary>
    internal int ComponentDepth { get; private set; }

    /// <summary>Whether an <see cref="Primitives.InFlow"/> is above this position, so a component
    /// that builds alone, outside its parent's walk, still builds with the intent armed.</summary>
    internal bool UnderInFlow { get; private set; }

    /// <summary>The nearest inherited position above this one, where a lookup by a component below it
    /// starts: read from the parent when this position mounts, and again when a global key moves it
    /// (<see cref="Reattach"/>).</summary>
    internal InheritedElement? NearestInherited { get; private set; }

    /// <summary>The reconciliation that last claimed this position (see <see cref="NodeElement"/>):
    /// a stamp rather than a flag, so nothing has to be cleared between two of them.</summary>
    internal int MatchStamp;

    /// <summary>The element a node is held by: a component builds, an inherited value holds its child and
    /// what reads it, every other node holds children.</summary>
    internal static Element Create(VisualNode configuration) => configuration switch
    {
        UiComponent component => new ComponentElement(component),
        InheritedComponent inherited => new InheritedElement(inherited),
        _ => new NodeElement(configuration),
    };

    /// <summary>
    /// Flutter's <c>canUpdate</c>: a position keeps its element when the next node is of the same
    /// runtime type and carries the same key, and is replaced otherwise.
    /// </summary>
    internal static bool CanUpdate(VisualNode current, VisualNode next) =>
        current.GetType() == next.GetType() && string.Equals(current.Key, next.Key, StringComparison.Ordinal)
        && ReferenceEquals(current.GlobalKey, next.GlobalKey);

    /// <summary>Takes its place under <paramref name="parent"/> and builds what is below it.</summary>
    internal void Mount(BuildOwner owner, Element? parent, int slot)
    {
        Owner = owner;
        Parent = parent;
        Slot = slot;
        Id = owner.NextId();
        Depth = parent is null ? 0 : parent.Depth + 1;
        ComponentDepth = parent is null ? 0 : parent.ComponentDepth + (parent is ComponentElement ? 1 : 0);
        UnderInFlow = parent is not null && (parent.UnderInFlow || parent.Configuration is InFlow);
        NearestInherited = parent as InheritedElement ?? parent?.NearestInherited;
        IsActive = true;
        if (Configuration.GlobalKey is { } key) owner.Hold(key, this);
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
    /// The child at <paramref name="slot"/> after a build: <paramref name="child"/> kept and updated when
    /// it can take <paramref name="configuration"/>, replaced when it cannot, and taken out when there is
    /// no node for the slot any more.
    /// </summary>
    private protected Element? UpdateChild(Element? child, VisualNode? configuration, int slot, bool force)
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
                child.Slot = slot;
                child.Update(configuration, force);
                return child;
            }

            Owner.Deactivate(child);
        }

        return Inflate(configuration, slot);
    }

    /// <summary>
    /// A position under this one, at <paramref name="slot"/>: a new one mounted and built, or, for a node
    /// whose <see cref="VisualNode.GlobalKey"/> a position left with earlier in this build, that position,
    /// moved here with everything below it and built with the node (#797).
    /// </summary>
    private protected Element Inflate(VisualNode configuration, int slot)
    {
        if (configuration.GlobalKey is { } key && Owner.Retake(key, configuration, this) is { } moved)
        {
            moved.Reattach(this, slot);
            moved.Update(configuration, force: true);
            return moved;
        }

        var created = Create(configuration);
        created.Mount(Owner, this, slot);
        return created;
    }

    /// <summary>
    /// Takes its place under <paramref name="parent"/> at <paramref name="slot"/>, having left another in
    /// this build: what it read from where it stood (its depth, the components and the inherited value
    /// above it) is read again here, for it and everything below it. Its id stays: it is the same position.
    /// </summary>
    internal void Reattach(Element parent, int slot)
    {
        Parent = parent;
        Slot = slot;
        IsActive = true;
        Rebase();
    }

    private void Rebase()
    {
        var parent = Parent;
        Depth = parent is null ? 0 : parent.Depth + 1;
        ComponentDepth = parent is null ? 0 : parent.ComponentDepth + (parent is ComponentElement ? 1 : 0);
        UnderInFlow = parent is not null && (parent.UnderInFlow || parent.Configuration is InFlow);
        NearestInherited = parent as InheritedElement ?? parent?.NearestInherited;
        OnRebased();
        VisitChildren(child => child.Rebase());
    }

    /// <summary>What a kind does when a global key moved it: a component lets go of the values it read
    /// where it stood, and reads them again from where it stands, as Flutter's <c>activate</c> does.</summary>
    private protected virtual void OnRebased()
    {
    }

    /// <summary>Whether this position is <paramref name="element"/> or stands below it.</summary>
    internal bool IsAtOrBelow(Element element)
    {
        for (Element? at = this; at is not null; at = at.Parent)
            if (ReferenceEquals(at, element)) return true;
        return false;
    }

    /// <summary>Lets go of <paramref name="child"/>, which a global key took to another parent in this
    /// build, without taking it out: the slot it held is empty until this position builds again.</summary>
    internal abstract void ForgetChild(Element child);

    /// <summary>Builds again a slot a global key emptied (<see cref="ForgetChild"/>) when this position's
    /// node still places something there: the build that moved the key never reached this position.</summary>
    internal abstract void FillForgotten();

    /// <summary>
    /// Takes this position and everything below it out of the tree, the children first: Flutter's
    /// order, so a component lets go of what it holds before the one that built it does.
    /// </summary>
    internal void Deactivate()
    {
        // A position with a global key leaves whole, and waits for the build to end: a node of its key
        // placed elsewhere in the same build takes it, and what is below it, there (#797).
        if (Configuration.GlobalKey is { } key && Owner.Park(key, this))
        {
            IsActive = false;
            return;
        }

        Leave();
    }

    /// <summary>Takes this position and everything below it out of the tree, whatever it carries.</summary>
    internal void Leave()
    {
        DeactivateChildren();
        IsActive = false;
        if (Configuration.GlobalKey is { } key) Owner.LetGo(key, this);
        OnDeactivated();
    }

    private protected abstract void DeactivateChildren();

    /// <summary>What leaving means for this kind beyond its children (a component's unmount).</summary>
    private protected virtual void OnDeactivated()
    {
    }
}
