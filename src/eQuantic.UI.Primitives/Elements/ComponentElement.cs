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
[ServerOnly]
public sealed class ComponentElement : Element
{
    private Element? _child;

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

    /// <inheritdoc />
    public override Element? ChildAt(int slot) => slot == 0 ? _child : null;

    private protected override void FirstBuild()
    {
        if (Owner.Live && Component is StatefulComponent stateful && stateful.Place(this))
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
        var built = BuildContained();
        _child = UpdateChild(_child, built, force);
    }

    private VisualNode BuildContained()
    {
        // The intent an InFlow arms while its subtree builds. Written only where it differs from
        // what is armed, because an AsyncLocal write allocates and nearly every build has none.
        var armed = InFlow.Current;
        if (armed == UnderInFlow)
            return ComponentBoundary.BuildAtDepth(Component, Owner.Context, ComponentDepth);

        InFlow.Current = UnderInFlow;
        try
        {
            return ComponentBoundary.BuildAtDepth(Component, Owner.Context, ComponentDepth);
        }
        finally
        {
            InFlow.Current = armed;
        }
    }

    /// <summary>Asks for this position to build again: a <c>SetState</c>, through the instance. The
    /// owner counts the request even when the position is already waiting to build
    /// (<see cref="BuildOwner.Marks"/>).</summary>
    internal void MarkNeedsBuild()
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
        // One instance a parent placed at two positions stays mounted until the last of them leaves.
        if (Owner.Live && Component is StatefulComponent stateful && stateful.Leave(this))
            Owner.QueueUnmount(stateful);
    }
}
