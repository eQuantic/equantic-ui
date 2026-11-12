namespace eQuantic.UI.Primitives;

/// <summary>
/// The position of a component: it builds, and what it built is its one child.
/// <para>
/// A <see cref="StatefulComponent"/> is RETAINED here. A parent's build makes a fresh instance every
/// time; the element keeps the first one it mounted, and each fresh one only hands over its
/// configuration through <see cref="UiComponent.AdoptConfig"/>, which is what the instance store did
/// with a string for a key. A <see cref="StatelessComponent"/> has no state to keep, so the element
/// simply holds the latest.
/// </para>
/// </summary>
public sealed class ComponentElement : Element
{
    private Element? _child;
    private ComponentContext? _context;
    private HashSet<InheritedElement>? _dependencies;

    internal ComponentElement(UiComponent component) : base(component)
    {
        Component = component;
    }

    /// <summary>The instance that builds at this position: the retained one for a stateful component,
    /// which is not the node a parent's latest build holds (<see cref="Element.Configuration"/>).</summary>
    public UiComponent Component { get; private set; }

    /// <summary>Whether a <c>SetState</c> asked this position to build again.</summary>
    internal bool Dirty { get; private set; }

    /// <summary>The owner's frame this position last built in. A position builds once a frame: one
    /// its parent's build already rebuilt is passed over by the dirty list, and a <c>SetState</c>
    /// from inside that build waits for the next frame.</summary>
    internal int BuiltInFrame { get; private set; }

    /// <summary>Whether a value this position read through <c>DependOn</c> changed since it last built:
    /// a stateful component hears it (<c>OnDependenciesChanged</c>) before it builds again.</summary>
    internal bool DependenciesChanged { get; private set; }

    /// <summary>Where this position stands among those that read what its owner's frame hands every build
    /// (the theme, the density, the type scale), so a frame that hands others builds it again; -1 for
    /// one that read none of it.</summary>
    internal int FrameReaderIndex { get; set; } = -1;

    /// <summary>
    /// What this position builds with: ONE context for as long as the position lives, a view of where it
    /// stands (docs/ELEMENT-TREE-PLAN.md, 3.3). It reads the frame's theme, density and type scale from
    /// the owner, and the values a subtree holds from the inherited positions above it.
    /// </summary>
    internal ComponentContext Context => _context ??= new ComponentContext(this);

    /// <summary>
    /// The nearest inherited value above this position that <paramref name="matches"/>, with this position
    /// registered as a dependent of it, so it builds again when the value changes. A predicate rather than
    /// a type argument, because the engine is written once and a type argument does not reach the browser.
    /// </summary>
    internal InheritedComponent? DependOn(Func<InheritedComponent, bool> matches)
    {
        var found = Nearest(matches);
        if (found is null) return null;
        // A position that left the tree (a handler that kept its context) reads, and registers nothing.
        if (!IsActive) return found.Inherited;
        found.AddDependent(this);
        (_dependencies ??= new HashSet<InheritedElement>()).Add(found);
        return found.Inherited;
    }

    /// <summary>The nearest inherited value above this position that <paramref name="matches"/>, read once:
    /// this position does not build again when it changes.</summary>
    internal InheritedComponent? Find(Func<InheritedComponent, bool> matches) => Nearest(matches)?.Inherited;

    private InheritedElement? Nearest(Func<InheritedComponent, bool> matches)
    {
        for (var at = NearestInherited; at is not null; at = at.Above)
        {
            if (matches(at.Inherited)) return at;
        }

        return null;
    }

    /// <summary>A value this position depends on changed: it builds again in this frame.</summary>
    internal void DependencyChanged()
    {
        if (!IsActive) return;
        DependenciesChanged = true;
        if (Dirty) return;
        Dirty = true;
        Owner.ScheduleDependent(this);
    }

    /// <inheritdoc />
    public override Element? ChildAt(int slot) => slot == 0 ? _child : null;

    /// <inheritdoc />
    public override void VisitChildren(Action<Element> visitor)
    {
        if (_child is not null) visitor(_child);
    }

    private protected override void FirstBuild()
    {
        if (Owner.Live && Component is StatefulComponent stateful && stateful.EnterPosition(this))
            Owner.QueueMount(this);

        Rebuild(force: false);
    }

    internal override void Update(VisualNode configuration, bool force)
    {
        if (ReferenceEquals(configuration, Configuration))
        {
            if (!force && !Dirty) return;
        }
        else
        {
            Configuration = configuration;
            var next = (UiComponent)configuration;
            // The retained instance stays and LEARNS the new configuration; a stateless one is
            // replaced, since nothing about it outlives the build that made it.
            if (Component is StatefulComponent retained)
            {
                if (!ReferenceEquals(retained, next)) retained.AdoptConfig(next);
            }
            else
            {
                Component = next;
            }
        }

        Rebuild(force);
    }

    /// <summary>
    /// Builds, and hands what was built to the child position. Through the boundary, so a throw is
    /// contained here exactly as it was in measure, and at this position's nesting, so a component
    /// that builds itself stops at the bound instead of overflowing the stack.
    /// </summary>
    internal void Rebuild(bool force)
    {
        // Cleared BEFORE the build, so a SetState from inside it marks the position again rather
        // than being lost to a flag that was about to be reset.
        Dirty = false;
        BuiltInFrame = Owner.Frame;
        if (DependenciesChanged)
        {
            DependenciesChanged = false;
            if (Component is StatefulComponent stateful) stateful.NotifyDependenciesChanged();
        }

        var built = BuildContained();
        _child = UpdateChild(_child, built, force);
    }

    // With this position's own context, which answers whether an InFlow is above it from the tree
    // (Element.UnderInFlow) rather than from an ambient channel armed around the build.
    private VisualNode BuildContained() => Owner.BuildComponent(Component, Context, ComponentDepth);

    /// <summary>Asks for this position to build again: a <c>SetState</c>, through the instance. The
    /// owner counts the request even when the position is already waiting to build
    /// (<see cref="BuildOwner.Marks"/>).
    /// <para>
    /// Public for a HOST that changed what a component reads without a <c>SetState</c>, as Flutter's
    /// <c>markNeedsBuild</c> is: a prefetch that loaded a component's fields marks its position, and
    /// only that subtree builds again, with the instance the position keeps (docs/ELEMENT-TREE-PLAN.md
    /// 3.7). Rebuilding everything instead hands a stateless component's position the fresh instance
    /// its parent made, and what the old one loaded is gone.
    /// </para>
    /// </summary>
    public void MarkNeedsBuild()
    {
        if (!IsActive) return;
        Owner.CountMark();
        if (Dirty) return;
        Dirty = true;
        Owner.Schedule(this);
    }

    private protected override void DeactivateChildren()
    {
        if (_child is null) return;
        _child.Deactivate();
        _child = null;
    }

    private protected override void OnDeactivated()
    {
        Owner.ForgetFrameReader(this);
        if (_dependencies is not null)
        {
            foreach (var dependency in _dependencies) dependency.RemoveDependent(this);
            _dependencies = null;
        }

        // One instance a parent placed at two positions stays mounted until the last of them leaves.
        if (Owner.Live && Component is StatefulComponent stateful && stateful.LeavePosition(this))
            Owner.QueueUnmount(stateful);
    }
}
