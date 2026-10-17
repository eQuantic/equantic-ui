namespace eQuantic.UI.Primitives;

/// <summary>
/// The owner of one element tree: what is dirty, the build phase that builds it, and the order its
/// components hear that they entered or left it. Flutter's <c>BuildOwner</c>
/// (docs/ELEMENT-TREE-PLAN.md, 3.6).
/// <para>
/// A LIVE owner is a host's, kept for as long as the surface it draws: it retains instances across
/// frames, runs <c>OnMount</c> and <c>OnUnmount</c>, and asks its host for a frame when a
/// <c>SetState</c> marks a position. A STRUCTURAL one (<see cref="Structural"/>) is a tree laid out
/// once, by a test or a tool: it builds the same tree and calls neither hook, because a hook there
/// would subscribe, set state or start work for a tree nobody draws again.
/// </para>
/// <para>
/// A frame is <see cref="Build"/>, then the realizer's layout and paint (which may show deferred
/// slots, <see cref="NodeElement.ShowDeferred"/>), then <see cref="EndFrame"/>, where what leaves
/// unmounts before what arrives mounts.
/// </para>
/// </summary>
[ServerOnly]
public sealed class BuildOwner
{
    private readonly List<ComponentElement> _dirty = new();
    private readonly List<ComponentElement> _dirtyNextFrame = new();
    private readonly List<ComponentElement> _mounting = new();
    private readonly List<StatefulComponent> _unmounting = new();
    private readonly List<NodeElement> _deferred = new();
    private ComponentContext? _context;
    private int _building;
    private int _stamp;

    /// <summary>A live owner: the one a host keeps for the surface it draws.</summary>
    public BuildOwner()
        : this(live: true)
    {
    }

    private BuildOwner(bool live)
    {
        Live = live;
    }

    /// <summary>An owner for a tree laid out once: it builds, and calls no lifecycle hook.</summary>
    public static BuildOwner Structural() => new(live: false);

    /// <summary>Asked when a position becomes dirty outside a build: the host's cue to draw a frame.
    /// Raised on the thread the <c>SetState</c> ran on, which the dispatcher makes the UI thread.</summary>
    public event Action? BuildRequested;

    /// <summary>The tree's root position, once it has been built.</summary>
    public Element? Root { get; private set; }

    /// <summary>Which frame this is: counted by <see cref="Build"/>.</summary>
    internal int Frame { get; private set; }

    /// <summary>Whether this frame rebuilds every component rather than the dirty ones: a deferred
    /// slot shown in it rebuilds everything below it too.</summary>
    internal bool RebuildingAll { get; private set; }

    /// <summary>Whether a position is waiting to build.</summary>
    public bool HasDirty => _dirty.Count > 0 || _dirtyNextFrame.Count > 0;

    internal bool Live { get; }

    /// <summary>The window size class this frame resolves an adaptive node's arm against.</summary>
    internal WindowSizeClass SizeClass { get; private set; }

    /// <summary>The context every component of this frame builds with.</summary>
    internal ComponentContext Context =>
        _context ?? throw new InvalidOperationException("A component built before its owner's frame began.");

    /// <summary>The table a list's keyed members are claimed through (see <see cref="NodeElement"/>),
    /// one for the whole tree because the claiming never runs inside another.</summary>
    internal Dictionary<string, Element> KeyScratch { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The build phase of a frame. With <paramref name="rebuildAll"/>, every position is walked and
    /// every component builds, which is what a frame after input does; without it, only the dirty
    /// positions build, shallowest first, and a frame nothing marked builds nothing.
    /// </summary>
    /// <returns>The root's position, for the layout to measure against.</returns>
    public Element Build(VisualNode root, ComponentContext context, WindowSizeClass sizeClass, bool rebuildAll)
    {
        _context = context;
        SizeClass = sizeClass;
        Frame++;
        RebuildingAll = rebuildAll;
        EnterBuild();
        try
        {
            if (Root is null || !Element.CanUpdate(Root.Configuration, root))
            {
                if (Root is not null) Deactivate(Root);
                Root = Element.Create(root);
                Root.Mount(this, null);
            }
            else if (rebuildAll || !ReferenceEquals(Root.Configuration, root))
            {
                Root.Update(root, rebuildAll);
            }

            // A frame that rebuilt everything has nothing left to build: what the walk did not
            // reach is in a deferred slot, which builds when it is shown or leaves when it is not.
            if (rebuildAll) _dirty.Clear();
            else BuildDirty();
        }
        finally
        {
            ExitBuild();
        }

        return Root;
    }

    /// <summary>
    /// Ends the frame: takes out the deferred slots it did not show, then runs every
    /// <c>OnUnmount</c> before any <c>OnMount</c>, the order that lets two components hand one
    /// exclusive resource across a swap. A <c>SetState</c> from a hook asks for the next frame.
    /// </summary>
    public void EndFrame()
    {
        for (var i = _deferred.Count - 1; i >= 0; i--)
        {
            var holder = _deferred[i];
            if (!holder.IsActive)
            {
                _deferred.RemoveAt(i);
                continue;
            }

            holder.SweepDeferred(Frame);
        }

        RebuildingAll = false;
        FlushLifecycle();

        // What a build marked waits for the next frame rather than running again in this one: a
        // component that sets its own state while it builds would otherwise never let a frame end.
        if (_dirtyNextFrame.Count == 0) return;
        foreach (var element in _dirtyNextFrame) _dirty.Add(element);
        _dirtyNextFrame.Clear();
        BuildRequested?.Invoke();
    }

    /// <summary>Takes the whole tree down: every component still in it is told it left, once. What a
    /// host calls when the surface it drew for is gone for good.</summary>
    public void UnmountAll()
    {
        if (Root is { } root)
        {
            Deactivate(root);
            Root = null;
        }

        _deferred.Clear();
        _dirty.Clear();
        _dirtyNextFrame.Clear();
        _mounting.Clear();
        FlushLifecycle();
    }

    internal void EnterBuild() => _building++;

    internal void ExitBuild() => _building--;

    internal int NextStamp() => ++_stamp;

    internal void Deactivate(Element element) => element.Deactivate();

    internal void QueueMount(ComponentElement element) => _mounting.Add(element);

    internal void QueueUnmount(StatefulComponent component) => _unmounting.Add(component);

    internal void TrackDeferred(NodeElement element) => _deferred.Add(element);

    /// <summary>A position marked dirty: built in this frame's phase when it is not running yet, in
    /// the next frame's when it is.</summary>
    internal void Schedule(ComponentElement element)
    {
        if (_building > 0)
        {
            _dirtyNextFrame.Add(element);
            return;
        }

        _dirty.Add(element);
        BuildRequested?.Invoke();
    }

    /// <summary>
    /// Flutter's <c>buildScope</c>: the dirty positions by depth, so a parent that rebuilds hands its
    /// children their configuration before they would build with a stale one, and a child its
    /// parent already rebuilt (no longer dirty) or took out (no longer active) is passed over.
    /// </summary>
    private void BuildDirty()
    {
        if (_dirty.Count == 0) return;
        _dirty.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));
        for (var i = 0; i < _dirty.Count; i++)
        {
            var element = _dirty[i];
            if (element.IsActive && element.Dirty) element.Rebuild(force: false);
        }

        _dirty.Clear();
    }

    private void FlushLifecycle()
    {
        foreach (var component in _unmounting) component.NotifyUnmounted();
        _unmounting.Clear();

        foreach (var element in _mounting)
        {
            // An instance that arrived and left in the same frame never entered the tree, and an
            // element replaced since it queued is not the one the instance belongs to.
            if (element.IsActive && element.Component is StatefulComponent stateful
                                 && ReferenceEquals(stateful.MountedAt, element))
                stateful.NotifyMounted();
        }

        _mounting.Clear();
    }
}
