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
/// How a component builds is handed in by whoever makes the owner, as Flutter's owner is handed
/// <c>onBuildScheduled</c>: the engine runs in the browser too (3.9), so it names no host's error
/// boundary. On .NET, <see cref="ComponentBoundary.Owner"/> and <see cref="ComponentBoundary.StructuralOwner"/>
/// hand it the boundary every target builds through, with its containment, report and depth bound.
/// </para>
/// <para>
/// A frame is <see cref="Build"/>, then the realizer's layout and paint (which may show deferred
/// slots, <see cref="NodeElement.ShowDeferred"/>), then <see cref="EndFrame"/>, where what leaves
/// unmounts before what arrives mounts. A realizer that draws only parts of the tree in a frame says
/// which ones (<see cref="DrewOnly"/>), so the frame judges the layers it drew and no other.
/// </para>
/// </summary>
public sealed class BuildOwner
{
    private readonly List<ComponentElement> _dirty = new();
    private readonly List<ComponentElement> _dirtyNextFrame = new();
    private readonly List<ComponentElement> _dirtyDeferred = new();
    private readonly List<ComponentElement> _deferredScratch = new();
    private readonly List<ComponentElement> _mounting = new();
    private readonly List<StatefulComponent> _unmounting = new();
    private readonly List<NodeElement> _deferred = new();
    // The positions that read what a frame hands every build (the theme, the density, the type scale),
    // each knowing its index here, so one that leaves is taken out in one step.
    private readonly List<ComponentElement> _frameReaders = new();
    private readonly List<ComponentElement> _routeReaders = new();
    private ComponentContext? _context;
    private int _building;
    // Whether the build phase of a frame is running, and which dirty position it is building: a value a
    // build changed marks its dependents into this frame's list, behind the one building.
    private bool _inFrameBuild;
    private int _dirtyCursor = -1;
    // The layer the realizer is showing, and which of its marked positions BuildDeferred is building.
    private NodeElement? _showing;
    private int _deferredCursor = -1;
    // The parts of the tree this frame's realizer drew, when it drew only parts of it (DrewOnly).
    private IReadOnlyList<Element>? _drewOnly;
    private int _stamp;
    private readonly ElementIds _ids;
    private readonly Func<UiComponent, ComponentContext, int, VisualNode> _buildComponent;

    /// <summary>A live owner: the one a host keeps for the surface it draws.</summary>
    /// <param name="buildComponent">How a component builds: its <c>Build</c> for a context, at a depth
    /// of components, inside whatever boundary contains what it throws.</param>
    /// <param name="holdsEveryArm">Whether an adaptive node's position holds every arm the node declares
    /// (<see cref="HoldsEveryArm"/>).</param>
    /// <param name="ids">The count the tree takes its ids from, shared with the other trees of a host
    /// that names their nodes in one space; a count of the tree's own when none is handed.</param>
    public BuildOwner(Func<UiComponent, ComponentContext, int, VisualNode> buildComponent, bool holdsEveryArm = false, ElementIds? ids = null)
        : this(buildComponent, holdsEveryArm, ids, live: true)
    {
    }

    // LIVE LAST, so the twin's one constructor, which is this one's shape, takes a public call's
    // arguments where they stand: JavaScript has one constructor, its signature is the widest, and the
    // public form passes `holdsEveryArm` and `ids` second and third.
    private BuildOwner(Func<UiComponent, ComponentContext, int, VisualNode> buildComponent, bool holdsEveryArm, ElementIds? ids, bool live)
    {
        ArgumentNullException.ThrowIfNull(buildComponent);
        _buildComponent = buildComponent;
        _ids = ids ?? new ElementIds();
        Live = live;
        HoldsEveryArm = holdsEveryArm;
    }

    /// <summary>An owner for a tree laid out once: it builds, and calls no lifecycle hook.</summary>
    /// <param name="buildComponent">How a component builds, as the live owner takes it.</param>
    /// <param name="holdsEveryArm">As the live owner takes it.</param>
    /// <param name="ids">The count the tree takes its ids from, as the live owner takes it.</param>
    public static BuildOwner Structural(Func<UiComponent, ComponentContext, int, VisualNode> buildComponent, bool holdsEveryArm = false, ElementIds? ids = null) =>
        new(buildComponent, holdsEveryArm, ids, live: false);

    /// <summary>
    /// The position whose component is building now, through the build this owner was handed, and null
    /// between builds. What a host reads in that build to know which position it is for: the server's
    /// data for a component is handed to it by where it stands (<see cref="Element.SlotsBelow"/>), before
    /// its build reads its fields.
    /// </summary>
    public ComponentElement? Building { get; private set; }

    /// <summary>Builds <paramref name="position"/>'s component the way this owner was handed.</summary>
    internal VisualNode BuildComponent(ComponentElement position)
    {
        var outer = Building;
        Building = position;
        try
        {
            return _buildComponent(position.Component, position.Context, position.ComponentDepth);
        }
        finally
        {
            Building = outer;
        }
    }

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

    /// <summary>
    /// How many times a <c>SetState</c> has asked a position of this tree to build, counted whether
    /// or not the position was already waiting to. <see cref="BuildRequested"/> is raised once for a
    /// position however often it is asked before its frame, so it cannot say whether a given handler
    /// asked: a host that hands input to an app's handler reads this before and after it, and a
    /// handler that asked for nothing is one that changed what it shows without saying so (#976).
    /// </summary>
    public int Marks { get; private set; }

    internal void CountMark() => Marks++;

    /// <summary>Whether this owner's tree is drawn again: a host's is, and calls the lifecycle hooks; a
    /// structural one (<see cref="Structural"/>) is laid out once and calls none, nor anything else
    /// that would set state or start work for a tree nobody draws again.</summary>
    public bool Live { get; }

    /// <summary>
    /// Whether an adaptive node's position holds EVERY arm the node declares, compact at slot 0, medium
    /// at 1 and expanded at 2, rather than the one arm the frame's width resolves. A realizer that
    /// draws every arm behind a gate of its own, as the web does with its media rules, needs each arm
    /// it draws to be a position, or the arm on screen at some width would be one no element holds and
    /// its stateful components would start again on every build. Photon draws one arm, and resolves it,
    /// at the slot it holds beside the others, and so does the browser once a page served with every arm
    /// has hydrated (#1783): set to false, the next frame that rebuilds every position takes the arms the
    /// width hides out, and keeps the one it shows where it stood.
    /// </summary>
    public bool HoldsEveryArm { get; set; }

    /// <summary>How wide the window is this frame, in dp: what an adaptive node's arm is resolved against, at
    /// the node's own thresholds (<see cref="AdaptiveNode.ResolveWidth"/>), as the web's gates and Photon's
    /// layout resolve it (#1465).</summary>
    internal float WindowWidth { get; private set; }

    /// <summary>The context this frame was handed: the theme, density, type scale and measurer every
    /// position's own context reads (<see cref="ComponentElement.Context"/>).</summary>
    internal ComponentContext Context =>
        _context ?? throw new InvalidOperationException("A component built before its owner's frame began.");

    /// <summary>
    /// Where a destination a component asks for goes: <c>Navigator.Of(context).Go(…)</c> reaches the host
    /// of the tree the component is in, so two windows in one process each navigate their own. Null when
    /// the host navigates nowhere, and a request is dropped.
    /// </summary>
    public Action<string>? NavigationRequested { get; set; }

    /// <summary>
    /// Which stateful instances a position KEEPS when its parent's build hands it a fresh one: every one
    /// when null, which is what a tree drawn for a person always answers, and otherwise only those this
    /// answers true for, while every other one gives the position to the fresh instance.
    /// <para>
    /// The server's data rounds answer it while they run (<c>ServerDataRounds</c>): only a component that
    /// loads data keeps its instance, since what it loaded is the page's data, and every other one takes
    /// what its parent hands it now. A section the page built before its prefetch ran was handed the
    /// values the page had then, and kept them: the page drew its own loaded text beside a section of
    /// empty labels, and nothing in the build said why (#1776). Between rounds nothing has happened to an
    /// instance that loads nothing, so the fresh one is the one the browser builds from the same data.
    /// </para>
    /// </summary>
    public Func<StatefulComponent, bool>? KeepsInstance { get; set; }

    /// <summary>The table a list's keyed members are claimed through (see <see cref="NodeElement"/>),
    /// one for the whole tree because the claiming never runs inside another.</summary>
    internal Dictionary<string, Element> KeyScratch { get; } = new(StringComparer.Ordinal);

    /// <summary>The positions that carry each <see cref="GlobalKey"/> in this tree (#797): one, where a
    /// single arm is held; one per arm where every arm is, the web's arms each standing for itself.</summary>
    private readonly Dictionary<GlobalKey, List<Element>> _held = new();

    /// <summary>The positions with a <see cref="GlobalKey"/> that left during this build, whole, until a node
    /// of their key takes one elsewhere or the build ends and they leave for good.</summary>
    private readonly Dictionary<GlobalKey, Element> _parked = new();

    /// <summary>The positions a global key was placed at in this build, mounted or moved there, which no
    /// second node of the key takes again, and the parents a moved position was taken from while it still
    /// stood there.</summary>
    private readonly HashSet<Element> _placed = new();

    private readonly List<Element> _forgetful = new();

    /// <summary>Whether the build is past taking a parked position: what leaves then leaves for good.</summary>
    private bool _settlingParked;

    /// <summary>Records <paramref name="element"/> as a position of <paramref name="key"/>.</summary>
    internal void Hold(GlobalKey key, Element element)
    {
        if (!_held.TryGetValue(key, out var holders))
        {
            holders = new List<Element>();
            _held[key] = holders;
        }

        holders.Add(element);
        if (_inFrameBuild) _placed.Add(element);
    }

    /// <summary>Forgets <paramref name="element"/> as a position of <paramref name="key"/>: it left for good.</summary>
    internal void LetGo(GlobalKey key, Element element)
    {
        if (!_held.TryGetValue(key, out var holders)) return;
        holders.Remove(element);
        if (holders.Count == 0) _held.Remove(key);
    }

    /// <summary>
    /// Keeps <paramref name="element"/>, which carries <paramref name="key"/> and leaves its parent, for
    /// the rest of the frame's build, whole; false when it leaves for good: outside the frame's build (a
    /// layer the realizer shows, a tree taken down), while the build settles, or when another position of
    /// its key already waits.
    /// </summary>
    internal bool Park(GlobalKey key, Element element)
    {
        if (!_inFrameBuild || _settlingParked || _parked.ContainsKey(key)) return false;
        _parked[key] = element;
        return true;
    }

    /// <summary>
    /// The position a node of <paramref name="key"/> placed under <paramref name="parent"/> takes, or null
    /// for a new one: the position of the key that left earlier in this build, or, where one arm is held,
    /// the one that still stands elsewhere, taken from its parent as Flutter takes it (a node placed
    /// before the build reaches the parent it leaves, as a window narrowing back to an earlier arm does).
    /// A position moves once in a build: a second node of the key in the same frame starts again, and the
    /// developer is told.
    /// </summary>
    internal Element? Retake(GlobalKey key, VisualNode configuration, Element parent)
    {
        if (!_inFrameBuild) return null;
        if (_parked.TryGetValue(key, out var parked))
        {
            _parked.Remove(key);
            if (!Element.CanUpdate(parked.Configuration, configuration))
            {
                parked.Leave();
                return null;
            }

            _placed.Add(parked);
            return parked;
        }

        // Where every arm is held, each arm's position is its own, and a key repeated across them is the
        // design rather than a mistake.
        if (HoldsEveryArm || !_held.TryGetValue(key, out var holders) || holders.Count != 1) return null;
        var held = holders[0];
        if (!held.IsActive || held.Parent is not { } from || !Element.CanUpdate(held.Configuration, configuration)
            || parent.IsAtOrBelow(held))
            return null;
        if (_placed.Contains(held))
        {
            DevelopmentNotices.Say(
                $"{key} is placed at two positions in one frame: one of them starts again, with none of the "
                + "other's state. A global key names one position at a time.");
            return null;
        }

        from.ForgetChild(held);
        _forgetful.Add(from);
        _placed.Add(held);
        return held;
    }

    /// <summary>
    /// Ends what the global keys moved in this build: every position that left with a key no node took
    /// elsewhere leaves for good, so what it holds unmounts at the frame's end, as any position's does,
    /// and then a parent a position was taken from that is still in the tree and still places a node
    /// there builds that slot again (a key placed twice, whose second node starts again).
    /// </summary>
    private void SettleGlobalKeys()
    {
        if (_parked.Count > 0)
        {
            _settlingParked = true;
            try
            {
                var leaving = new List<Element>(_parked.Values);
                _parked.Clear();
                foreach (var element in leaving) element.Leave();
            }
            finally
            {
                _settlingParked = false;
            }
        }

        for (var i = 0; i < _forgetful.Count; i++)
        {
            if (_forgetful[i].IsActive) _forgetful[i].FillForgotten();
        }

        _forgetful.Clear();
        _placed.Clear();
    }

    /// <summary>
    /// The build phase of a frame. With <paramref name="rebuildAll"/>, every position is walked and
    /// every component builds, which is what a frame after input does; without it, only the dirty
    /// positions build, shallowest first, and a frame nothing marked builds nothing.
    /// </summary>
    /// <returns>The root's position, for the layout to measure against.</returns>
    public Element Build(VisualNode root, ComponentContext context, float windowWidth, bool rebuildAll)
    {
        var previous = _context;
        _context = context;
        WindowWidth = windowWidth;
        Frame++;
        // What the realizer drew is said of the frame that ends; a frame that never reached its end
        // hands nothing to this one.
        _drewOnly = null;
        RebuildingAll = rebuildAll;
        EnterBuild();
        _inFrameBuild = true;
        try
        {
            // What a frame hands every build changed: the positions that read it build again, and only
            // they, where a density switch used to rebuild every component (docs/ELEMENT-TREE-PLAN.md, 3.4).
            if (previous is not null && FrameValuesDiffer(previous, context)) MarkFrameReaders();
            // The route is read apart: a navigation within one page type builds what read the route, and
            // not every reader of the theme.
            if (previous is not null && !ReferenceEquals(previous.Route, context.Route)) MarkRouteReaders();

            if (Root is null || !Element.CanUpdate(Root.Configuration, root))
            {
                if (Root is not null) Deactivate(Root);
                Root = Element.Create(root);
                Root.Mount(this, null, 0);
            }
            else if (rebuildAll || !ReferenceEquals(Root.Configuration, root))
            {
                Root.Update(root, rebuildAll);
            }

            // After a walk that rebuilt everything, what it reached built in this frame and is passed
            // over; what it did not reach is under a deferred slot, which builds when it is shown.
            BuildDirty();
        }
        finally
        {
            // Settled even when the build throws, so a position a global key parked does not outlive it.
            try
            {
                SettleGlobalKeys();
            }
            finally
            {
                _inFrameBuild = false;
                ExitBuild();
            }
        }

        return Root;
    }

    private static bool FrameValuesDiffer(ComponentContext before, ComponentContext now) =>
        !ReferenceEquals(before.Theme, now.Theme) || before.Density != now.Density || before.TypeScale != now.TypeScale;

    /// <summary>A position read what the frame hands every build, and builds again when it changes.</summary>
    internal void ReadFrame(ComponentElement reader)
    {
        if (reader.FrameReaderIndex >= 0) return;
        reader.FrameReaderIndex = _frameReaders.Count;
        _frameReaders.Add(reader);
    }

    /// <summary>A position that read the frame left the tree: the last reader takes its place.</summary>
    internal void ForgetFrameReader(ComponentElement reader)
    {
        var index = reader.FrameReaderIndex;
        if (index < 0) return;
        var last = _frameReaders[_frameReaders.Count - 1];
        _frameReaders[index] = last;
        last.FrameReaderIndex = index;
        _frameReaders.RemoveAt(_frameReaders.Count - 1);
        reader.FrameReaderIndex = -1;
    }

    private void MarkFrameReaders()
    {
        foreach (var reader in _frameReaders) reader.DependencyChanged();
    }

    /// <summary>A position read the frame's route, and builds again when the frame hands another.</summary>
    internal void ReadRoute(ComponentElement reader)
    {
        if (reader.RouteReaderIndex >= 0) return;
        reader.RouteReaderIndex = _routeReaders.Count;
        _routeReaders.Add(reader);
    }

    /// <summary>A position that read the route left the tree: the last reader takes its place.</summary>
    internal void ForgetRouteReader(ComponentElement reader)
    {
        var index = reader.RouteReaderIndex;
        if (index < 0) return;
        var last = _routeReaders[_routeReaders.Count - 1];
        _routeReaders[index] = last;
        last.RouteReaderIndex = index;
        _routeReaders.RemoveAt(_routeReaders.Count - 1);
        reader.RouteReaderIndex = -1;
    }

    private void MarkRouteReaders()
    {
        foreach (var reader in _routeReaders) reader.DependencyChanged();
    }

    /// <summary>
    /// Says this frame's realizer drew only <paramref name="parts"/> of the tree, and none of it when the
    /// list is empty: the web's patch render, which lowers again only the components that built and
    /// leaves the rest of the page as it drew it (#1455). At <see cref="EndFrame"/>, a deferred slot
    /// outside those parts keeps its layer, which the page still shows; one inside them that the frame
    /// did not show is taken out, as in a frame that drew the whole tree. A frame whose realizer says
    /// nothing drew the whole tree, which is every frame Photon and the server draw (#1575).
    /// </summary>
    /// <param name="parts">The positions at the top of each part the realizer drew again.</param>
    public void DrewOnly(IReadOnlyList<Element> parts) => _drewOnly = parts;

    /// <summary>
    /// Whether a marked position is waiting for its layer to be shown, which builds it
    /// (<see cref="NodeElement.ShowDeferred"/>). What a realizer that draws only parts of the tree
    /// (<see cref="DrewOnly"/>) asks before the frame ends: a layer it does not draw is not shown, so what
    /// waits in it would wait for ever, and a frame that draws the whole tree is what builds it.
    /// </summary>
    public bool WaitsForALayer
    {
        get
        {
            foreach (var element in _dirtyDeferred)
            {
                if (element.IsActive && element.Dirty) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Ends the frame: takes out the deferred slots it did not show, among the parts of the tree it drew
    /// (<see cref="DrewOnly"/>), then runs every <c>OnUnmount</c> before any <c>OnMount</c>, the order
    /// that lets two components hand one exclusive resource across a swap. A <c>SetState</c> from a hook
    /// asks for the next frame.
    /// </summary>
    public void EndFrame()
    {
        var parts = _drewOnly;
        _drewOnly = null;
        for (var i = _deferred.Count - 1; i >= 0; i--)
        {
            var holder = _deferred[i];
            if (!holder.IsActive)
            {
                _deferred.RemoveAt(i);
                continue;
            }

            // A slot the frame did not draw is not one it chose not to show: its layer stays.
            if (parts is not null && !StandsIn(holder, parts)) continue;
            holder.SweepDeferred(Frame);
        }

        RebuildingAll = false;

        // What waited for a layer that was not shown left the tree with it. Anything else still
        // waiting builds in the next frame.
        foreach (var element in _dirtyDeferred)
        {
            if (element.IsActive && element.Dirty) _dirtyNextFrame.Add(element);
        }

        _dirtyDeferred.Clear();
        FlushLifecycle();

        // What a build marked waits for the next frame rather than running again in this one: a
        // component that sets its own state while it builds would otherwise never let a frame end.
        if (_dirtyNextFrame.Count == 0) return;
        foreach (var element in _dirtyNextFrame) _dirty.Add(element);
        _dirtyNextFrame.Clear();
        BuildRequested?.Invoke();
    }

    /// <summary>Whether <paramref name="element"/> stands at or below one of <paramref name="parts"/>.</summary>
    private static bool StandsIn(Element element, IReadOnlyList<Element> parts)
    {
        for (Element? at = element; at is not null; at = at.Parent)
        {
            foreach (var part in parts)
            {
                if (ReferenceEquals(at, part)) return true;
            }
        }

        return false;
    }

    /// <summary>Takes the whole tree down: every component still in it is told it left, once. What a
    /// host calls when the surface it drew for is gone for good.</summary>
    public void UnmountAll()
    {
        _drewOnly = null;
        if (Root is { } root)
        {
            Deactivate(root);
            Root = null;
        }

        _deferred.Clear();
        _dirty.Clear();
        _dirtyNextFrame.Clear();
        _dirtyDeferred.Clear();
        _mounting.Clear();
        FlushLifecycle();
    }

    internal void EnterBuild() => _building++;

    internal void ExitBuild() => _building--;

    /// <summary>The realizer shows <paramref name="holder"/>'s layer: a build outside the frame's build
    /// phase, whose dependents build in its <see cref="BuildDeferred"/>. One layer at a time: both
    /// realizers show a layer and return before they walk it, so a layer inside it is shown after.</summary>
    internal void EnterLayer(NodeElement holder)
    {
        EnterBuild();
        _showing = holder;
    }

    internal void ExitLayer()
    {
        _showing = null;
        ExitBuild();
    }

    internal int NextStamp() => ++_stamp;

    /// <summary>The next id of this tree (<see cref="Element.Id"/>, <see cref="NodeElement.PartId"/>),
    /// from the count it was handed.</summary>
    internal int NextId() => _ids.Next();

    internal void Deactivate(Element element) => element.Deactivate();

    internal void QueueMount(ComponentElement element) => _mounting.Add(element);

    internal void QueueUnmount(StatefulComponent component) => _unmounting.Add(component);

    internal void TrackDeferred(NodeElement element) => _deferred.Add(element);

    /// <summary>
    /// A position whose dependency changed: built in THIS frame when the change happened in its build
    /// phase, as Flutter's dependents are, since a value its parent's build changed is one the frame
    /// shows. Behind the position building, by depth, so a parent still hands it its configuration
    /// first. A change while the realizer shows a layer builds in that layer's
    /// <see cref="BuildDeferred"/>, before the layer is laid out (#1413). Anywhere else it waits like a
    /// <c>SetState</c>.
    /// </summary>
    internal void ScheduleDependent(ComponentElement element)
    {
        if (_showing is not null)
        {
            // While the layer's own marked positions build, behind the one building when it is this
            // layer's; before that, or for a layer inside it, the list BuildDeferred reads.
            if (_deferredCursor >= 0 && ReferenceEquals(DeferredHolderOf(element), _showing))
                InsertBehind(_deferredScratch, _deferredCursor, element);
            else
                _dirtyDeferred.Add(element);
            return;
        }

        if (!_inFrameBuild)
        {
            Schedule(element);
            return;
        }

        if (_dirtyCursor < 0)
        {
            // The walk before the dirty list: the list is sorted when it is read.
            _dirty.Add(element);
            return;
        }

        InsertBehind(_dirty, _dirtyCursor, element);
    }

    /// <summary>Into a list sorted by depth that is being built, behind the position at
    /// <paramref name="cursor"/>, in depth order.</summary>
    private static void InsertBehind(List<ComponentElement> list, int cursor, ComponentElement element)
    {
        var at = list.Count;
        while (at > cursor + 1 && list[at - 1].Depth > element.Depth) at--;
        list.Insert(at, element);
    }

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
    /// parent already rebuilt in this frame, or took out, is passed over.
    /// <para>
    /// A position under a DEFERRED slot waits instead: the slot takes its configuration only when the
    /// realizer shows the layer, so built here it would build with the old one and again with the new,
    /// or build for a layer that is closing. <see cref="BuildDeferred"/> builds it when the slot is
    /// shown, and the end of the frame lets it go with the slot when it is not.
    /// </para>
    /// </summary>
    private void BuildDirty()
    {
        if (_dirty.Count == 0) return;
        _dirty.Sort(ByDepth);
        for (var i = 0; i < _dirty.Count; i++)
        {
            _dirtyCursor = i;
            var element = _dirty[i];
            if (!WaitsToBuild(element)) continue;
            if (DeferredHolderOf(element) is not null)
            {
                _dirtyDeferred.Add(element);
                continue;
            }

            element.Rebuild(force: false);
        }

        _dirtyCursor = -1;
        _dirty.Clear();
    }

    /// <summary>
    /// The marked positions under <paramref name="holder"/>'s deferred slot, built once the slot has
    /// the configuration its layer shows, shallowest first. Only the ones whose NEAREST deferred slot
    /// is this one: a layer inside the layer waits for its own.
    /// </summary>
    internal void BuildDeferred(NodeElement holder)
    {
        if (_dirtyDeferred.Count == 0) return;
        var mine = _deferredScratch;
        mine.Clear();
        for (var i = _dirtyDeferred.Count - 1; i >= 0; i--)
        {
            var element = _dirtyDeferred[i];
            if (element.IsActive && !ReferenceEquals(DeferredHolderOf(element), holder)) continue;
            _dirtyDeferred.RemoveAt(i);
            if (element.IsActive) mine.Add(element);
        }

        mine.Sort(ByDepth);
        try
        {
            // By index: a dependent a build here marks joins the list (ScheduleDependent).
            for (var i = 0; i < mine.Count; i++)
            {
                _deferredCursor = i;
                if (WaitsToBuild(mine[i])) mine[i].Rebuild(force: false);
            }
        }
        finally
        {
            _deferredCursor = -1;
        }

        mine.Clear();
    }

    private static readonly Comparison<ComponentElement> ByDepth = static (a, b) => a.Depth.CompareTo(b.Depth);

    /// <summary>Marked, in the tree, and not built yet in this frame.</summary>
    private bool WaitsToBuild(ComponentElement element) =>
        element.IsActive && element.Dirty && element.BuiltInFrame != Frame;

    /// <summary>The nearest node whose deferred slot holds <paramref name="element"/>'s branch, or null
    /// for a position the build phase reaches.</summary>
    private static NodeElement? DeferredHolderOf(Element element)
    {
        for (var child = element; child.Parent is { } parent; child = parent)
        {
            if (parent is NodeElement node && node.HoldsDeferred(child)) return node;
        }

        return null;
    }

    private void FlushLifecycle()
    {
        foreach (var component in _unmounting) component.NotifyUnmounted();
        _unmounting.Clear();

        foreach (var element in _mounting)
        {
            // An instance that arrived and left in the same frame never entered the tree. One that
            // left the position it arrived at but still holds another did, and is mounted there.
            if (element.Component is StatefulComponent { MountedAt.IsActive: true } stateful)
                stateful.NotifyMounted();
        }

        _mounting.Clear();
    }
}
