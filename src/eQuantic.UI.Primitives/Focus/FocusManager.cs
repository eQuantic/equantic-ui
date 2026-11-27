namespace eQuantic.UI.Primitives;

/// <summary>
/// What a realizer drives with the focus, one per host (#1897): Flutter's <c>FocusManager</c> with its
/// <c>WidgetOrderTraversalPolicy</c>, which walks the focus tree's order and reads no geometry, written once and run
/// by every target (the browser runs its transpiled twin, as it runs <see cref="GestureRouter"/>), so two targets
/// cannot disagree on where Tab goes.
/// <list type="bullet">
/// <item><see cref="Attach"/>: the frame's stops, in the realizer's own tree order (Photon's paint order, the web's
/// document order), each with the focus nodes it stands under. The nodes' tree is made from them.</item>
/// <item><see cref="Focused"/>: the target moved the focus (a press, the browser, a field closing): the manager
/// adopts the position and every node whose focus changed hears it. A WRITE the target makes for its own reasons is
/// adopted, never a second order.</item>
/// <item><see cref="Traverse"/>: where Tab and Shift+Tab land. Every DECISION about where the focus goes is made here.</item>
/// <item><see cref="TakeRequest"/> and <see cref="RequestAsked"/>: what a node asked for between frames, applied by
/// the realizer after the write that follows it.</item>
/// </list>
/// <para>
/// The walk, Flutter's reading order without geometry: its scope is the innermost <see cref="FocusScopeNode"/> in the
/// chain of the stop that has the focus (the root when there is none), and the modal scope when that stop is outside
/// it; its candidates are the traversable stops under the scope, in the realizer's order; with no focused stop it
/// enters at the first going forward and at the last going back; past the last one the scope's
/// <see cref="TraversalEdgeBehavior"/> decides: <see cref="TraversalEdgeBehavior.ClosedLoop"/> wraps,
/// <see cref="TraversalEdgeBehavior.Stop"/> stays, <see cref="TraversalEdgeBehavior.ParentScope"/> goes on in the
/// enclosing scope after the place of this one. At the root a manager wraps, or, made with
/// <c>leavesViewAtRoot</c>, answers that the focus leaves the page. A modal scope bounds the walk: it wraps as a
/// closed loop whatever it says, and with no traversable stop it holds the keyboard.
/// </para>
/// </summary>
public sealed class FocusManager
{
    private readonly bool _leavesViewAtRoot;

    /// <summary>The chain of a position that stands under no node.</summary>
    private readonly IReadOnlyList<FocusAttachment> _none = new List<FocusAttachment>();

    /// <summary>The frame's stops, in the realizer's order.</summary>
    private IReadOnlyList<TraversalStop> _stops = new List<TraversalStop>();

    /// <summary>The topmost modal scope of the frame, or null.</summary>
    private FocusScopeNode? _modal;

    /// <summary>The place of the modal scope itself, where the focus is held when it has no stop.</summary>
    private object? _modalPosition;

    /// <summary>The nodes the latest frame showed, and the list the frame before it filled, taken back for the next.</summary>
    private List<FocusNode> _nodes = new();
    private List<FocusNode> _spare = new();

    private int _frame;

    /// <summary>The position that holds the focus.</summary>
    private object? _primary;

    /// <summary>The nodes the focused position stands under, outermost first.</summary>
    private IReadOnlyList<FocusAttachment> _chain;

    /// <summary>The node that owns the focused position.</summary>
    private FocusNode? _primaryNode;

    /// <summary>What a node asked for since the last <see cref="TakeRequest"/>: the last ask wins.</summary>
    private FocusRequest? _request;

    /// <param name="leavesViewAtRoot">Whether a walk past the outermost scope leaves the page (the web, where the
    /// browser's own Tab takes the focus out of it, as Flutter's web embedding does) instead of wrapping to the
    /// first stop (Photon).</param>
    public FocusManager(bool leavesViewAtRoot)
    {
        _leavesViewAtRoot = leavesViewAtRoot;
        _chain = _none;
    }

    /// <summary>A node asked for the focus, for one to leave, or for it to move, between frames: the realizer owes the
    /// manager a <see cref="TakeRequest"/> after the write that follows, and a frame for it when none is due.</summary>
    public event Action? RequestAsked;

    /// <summary>The node that owns the stop holding the focus: Flutter's <c>primaryFocus</c>. A <c>Focus</c>
    /// around several stops is one node for all of them. Null when no stop holds the focus, or none has an owner.</summary>
    public FocusNode? PrimaryFocus => _primaryNode;

    /// <summary>The position that holds the focus, any stop's, or null.</summary>
    public object? PrimaryPosition => _primary;

    /// <summary>
    /// The frame's stops, in the order of the realizer's tree. The nodes' tree is made from their chains; a node a
    /// frame no longer shows leaves it, and a stop that held the focus and is gone leaves nothing focused, which
    /// notifies. A node shown for the first time with <c>autofocus</c> asks for the focus once, unless the focus is
    /// already inside its nearest scope and unless an ask is waiting; a node asked for before it was shown has its
    /// ask kept now, the later of them standing. A position is compared by identity.
    /// </summary>
    /// <param name="stops">The frame's stops.</param>
    /// <param name="modal">The scope of the topmost modal layer the frame shows, or null: the walk keeps inside it
    /// and no ask reaches a stop outside it. It stands in the chain of each stop under it.</param>
    /// <param name="modalPosition">Where the modal scope is, which holds the focus while it has no traversable stop:
    /// the layer's own element on the web; null on Photon, which has none and clears the focus.</param>
    public void Attach(IReadOnlyList<TraversalStop> stops, FocusScopeNode? modal = null, object? modalPosition = null)
    {
        _stops = stops;
        _modal = modal;
        _modalPosition = modalPosition;
        _frame++;
        var shown = _spare;
        shown.Clear();
        List<FocusNode>? wanting = null;
        foreach (var stop in stops)
        {
            FocusNode? parent = null;
            FocusScopeNode? scope = null;
            foreach (var attachment in stop.Chain)
            {
                var node = attachment.Node;
                if (node.Frame != _frame)
                {
                    var first = !ReferenceEquals(node.Manager, this);
                    Show(node, parent, scope, shown);
                    node.OnFocusChange = attachment.OnFocusChange;
                    if (first && attachment.Autofocus)
                    {
                        wanting ??= new List<FocusNode>();
                        wanting.Add(node);
                    }
                }
                parent = node;
                if (node is FocusScopeNode own) scope = own;
            }
        }
        if (modal != null && modal.Frame != _frame) Show(modal, null, null, shown);
        foreach (var node in _nodes)
        {
            if (node.Frame == _frame) continue;
            node.Manager = null;
            node.Parent = null;
            node.EnclosingScope = null;
            node.Children.Clear();
        }
        _spare = _nodes;
        _nodes = shown;
        foreach (var node in shown)
            if (node is FocusScopeNode scope && scope.FocusedChild is { } child && !ReferenceEquals(child.Manager, this))
                scope.FocusedChild = null;

        // A stop that held the focus and is gone leaves nothing focused.
        if (_primary != null && IndexOfStop(_primary) < 0 && !IsModalPosition(_primary)) _primary = null;
        Settle(ChainOf(_primary));

        // The ask of a node that was not shown yet, the later of them standing against one made since.
        FocusRequest? pending = null;
        foreach (var node in shown)
        {
            if (node.PendingAsk <= 0) continue;
            if (pending == null || node.PendingAsk > pending.Ticket)
                pending = new FocusRequest(FocusRequestKind.Focus, node, node.PendingAsk);
            node.PendingAsk = 0;
        }
        if (pending != null && (_request == null || pending.Ticket > _request.Ticket)) _request = pending;

        // Autofocus asks only while nothing else has, and the last of the positions shown now stands.
        if (_request != null || wanting == null) return;
        foreach (var node in wanting)
            if (!FocusIsInside(node.EnclosingScope)) _request = new FocusRequest(FocusRequestKind.Focus, node, 0);
    }

    /// <summary>
    /// The target moved the focus onto <paramref name="position"/>, or took it away (null): the manager adopts it,
    /// and every node whose <see cref="FocusNode.HasFocus"/> or <see cref="FocusNode.HasPrimaryFocus"/> changed
    /// raises <see cref="FocusNode.Changed"/> and calls its <c>onFocusChange</c> when <c>HasFocus</c> did, outermost
    /// first, whatever moved it: Tab, a press, a request, the browser, or the focused control leaving the tree.
    /// A position that is no stop of the frame holds the focus under no node.
    /// </summary>
    public void Focused(object? position)
    {
        _primary = position;
        Settle(ChainOf(position));
    }

    /// <summary>
    /// Where Tab (<paramref name="forward"/>) or Shift+Tab lands, from the stop that holds the focus. It decides and
    /// moves nothing: the realizer lands on the answer and reports it with <see cref="Focused"/>. When the answer
    /// leaves the page the browser's own Tab does it, and nothing is landed on.
    /// </summary>
    public FocusMove Traverse(bool forward)
    {
        var at = IndexOfStop(_primary);
        FocusScopeNode? scope = at >= 0 ? InnermostScope(_stops[at].Chain) : null;
        var entering = at < 0;
        if (_modal != null && (at < 0 || !Under(at, _modal)))
        {
            scope = _modal;
            entering = true;
        }
        if (entering)
        {
            var enter = forward ? NextTraversable(scope, -1) : PreviousTraversable(scope, _stops.Count);
            if (enter >= 0) return new FocusMove(_stops[enter].Position, false);
            // A modal scope with no stop holds the keyboard: nothing behind it takes it.
            return new FocusMove(scope != null && ReferenceEquals(scope, _modal) ? _modalPosition : null, false);
        }
        var next = forward ? NextTraversable(scope, at) : PreviousTraversable(scope, at);
        if (next >= 0) return new FocusMove(_stops[next].Position, false);
        return PastEdge(scope, forward ? LastUnder(scope) : FirstUnder(scope), forward);
    }

    /// <summary>
    /// The move that what a node asked for since the last call comes to, once: the last ask wins. A node's ask for
    /// the focus lands on its first stop that may take it, behind no modal scope; a scope's on its
    /// <see cref="FocusScopeNode.FocusedChild"/>, or its first traversable stop. An unfocus is a move to no stop.
    /// A node that cannot be found, one that may not take the focus, one behind the modal scope, a move that leaves
    /// the page and a move that ends where it began are no move at all, and answer null.
    /// </summary>
    public FocusMove? TakeRequest()
    {
        var request = _request;
        _request = null;
        if (request == null) return null;
        if (request.Kind == FocusRequestKind.Unfocus)
            return request.Node.HasFocus ? new FocusMove(null, false) : null;
        if (request.Kind == FocusRequestKind.Focus)
        {
            var at = Landing(request.Node);
            if (at < 0) return null;
            var position = _stops[at].Position;
            return ReferenceEquals(position, _primary) ? null : new FocusMove(position, false);
        }
        var move = Traverse(request.Kind == FocusRequestKind.Next);
        if (move.LeavesView) return null;
        if (move.To == null) return _primary == null ? null : move;
        return ReferenceEquals(move.To, _primary) ? null : move;
    }

    /// <summary>A node asked, and the later ask stands.</summary>
    internal void Ask(FocusRequest request)
    {
        _request = request;
        RequestAsked?.Invoke();
    }

    /// <summary><see cref="FocusNode.NextFocus"/> and <see cref="FocusNode.PreviousFocus"/>: the walk is asked now for
    /// its answer and again after the write that follows, for its move.</summary>
    internal bool AskToMove(FocusNode node, bool forward)
    {
        var move = Traverse(forward);
        Ask(new FocusRequest(forward ? FocusRequestKind.Next : FocusRequestKind.Previous, node, FocusNode.NextAsk()));
        return !move.LeavesView && move.To != null && !ReferenceEquals(move.To, _primary);
    }

    /// <summary>Puts <paramref name="node"/> in the frame's tree, under <paramref name="parent"/>.</summary>
    private void Show(FocusNode node, FocusNode? parent, FocusScopeNode? scope, List<FocusNode> shown)
    {
        node.Frame = _frame;
        node.Manager = this;
        node.Parent = parent;
        node.EnclosingScope = scope;
        node.Children.Clear();
        parent?.Children.Add(node);
        shown.Add(node);
    }

    /// <summary>Whether the focus is inside <paramref name="scope"/>, or, for no scope, anywhere.</summary>
    private bool FocusIsInside(FocusScopeNode? scope) => _primary != null && (scope == null || Contains(_chain, scope));

    /// <summary>
    /// The position now holds the focus under <paramref name="next"/>: every node that lost or gained it, or the
    /// ownership of it, is settled first and only then told, so a handler sees the state the report left, outermost
    /// first, the nodes that lost it before the ones that gained it at each depth. A report that changes no node
    /// tells nobody.
    /// </summary>
    private void Settle(IReadOnlyList<FocusAttachment> next)
    {
        var previous = _chain;
        var owner = OwnerOf(next);
        if (owner == null && next.Count > 0 && _modalPosition != null && ReferenceEquals(_primary, _modalPosition)) owner = _modal;
        if (SameNodes(previous, next) && ReferenceEquals(owner, _primaryNode))
        {
            _chain = next;
            return;
        }
        _chain = next;
        _primaryNode = owner;
        for (var i = 0; i + 1 < next.Count; i++)
            if (next[i].Node is FocusScopeNode scope) scope.FocusedChild = next[i + 1].Node;
        var events = new List<FocusEvent>();
        var seen = new List<FocusNode>();
        var depth = Math.Max(previous.Count, next.Count);
        for (var d = 0; d < depth; d++)
        {
            if (d < previous.Count) Observe(previous[d].Node, next, owner, seen, events);
            if (d < next.Count) Observe(next[d].Node, next, owner, seen, events);
        }
        foreach (var heard in events)
        {
            heard.Node.RaiseChanged();
            if (heard.FocusChanged) heard.Node.OnFocusChange?.Invoke(heard.HasFocus);
        }
    }

    /// <summary>One node of a report: what it settles on, and whether that is a change.</summary>
    private static void Observe(FocusNode node, IReadOnlyList<FocusAttachment> next, FocusNode? owner, List<FocusNode> seen,
        List<FocusEvent> events)
    {
        if (seen.Contains(node)) return;
        seen.Add(node);
        var hasFocus = Contains(next, node);
        var hasPrimary = ReferenceEquals(node, owner);
        var focusChanged = node.HasFocus != hasFocus;
        var changed = focusChanged || node.HasPrimaryFocus != hasPrimary;
        node.HasFocus = hasFocus;
        node.HasPrimaryFocus = hasPrimary;
        if (changed) events.Add(new FocusEvent(node, hasFocus, focusChanged));
    }

    /// <summary>The stop <paramref name="node"/> asks the focus to land on, or -1: the stops it owns, in order, then
    /// any stop under it, never one that may not take the focus nor one behind the modal scope. A scope lands on
    /// its focused child, else on its first traversable stop.</summary>
    private int Landing(FocusNode node)
    {
        if (!ReferenceEquals(node.Manager, this) || !node.CanRequestFocus) return -1;
        if (node is FocusScopeNode scope)
        {
            if (scope.FocusedChild is { } child && !ReferenceEquals(child, scope))
            {
                var inChild = Landing(child);
                if (inChild >= 0) return inChild;
            }
            for (var i = 0; i < _stops.Count; i++)
                if (InModal(i) && Under(i, scope) && Traversable(i)) return i;
            return -1;
        }
        for (var i = 0; i < _stops.Count; i++)
            if (InModal(i) && ReferenceEquals(OwnerOf(_stops[i].Chain), node)) return i;
        for (var i = 0; i < _stops.Count; i++)
            if (InModal(i) && Contains(_stops[i].Chain, node) && Requestable(i)) return i;
        return -1;
    }

    /// <summary>Past the edge of <paramref name="scope"/> going <paramref name="forward"/> or back, from
    /// <paramref name="edge"/>, the last (or first) stop under it: what its behaviour makes of it. The root wraps, or
    /// leaves the page.</summary>
    private FocusMove PastEdge(FocusScopeNode? scope, int edge, bool forward)
    {
        if (scope == null)
        {
            if (_leavesViewAtRoot) return new FocusMove(null, true);
            return Wrapped(null, forward);
        }
        var behavior = scope.TraversalEdgeBehavior;
        // A modal scope is the end of the walk: nothing beyond it, so it wraps whatever it says.
        if (ReferenceEquals(scope, _modal) && behavior == TraversalEdgeBehavior.ParentScope) behavior = TraversalEdgeBehavior.ClosedLoop;
        if (behavior == TraversalEdgeBehavior.Stop) return new FocusMove(_primary, false);
        if (behavior == TraversalEdgeBehavior.ClosedLoop) return Wrapped(scope, forward);
        var outer = ScopeOutside(_stops[IndexOfStop(_primary)].Chain, scope);
        var next = forward ? NextTraversable(outer, edge) : PreviousTraversable(outer, edge);
        if (next >= 0) return new FocusMove(_stops[next].Position, false);
        return PastEdge(outer, forward ? LastUnder(outer) : FirstUnder(outer), forward);
    }

    /// <summary>The first (going forward) or last traversable stop under <paramref name="scope"/>, where a walk that
    /// went past its edge lands when it wraps; where it is, when there is none.</summary>
    private FocusMove Wrapped(FocusScopeNode? scope, bool forward)
    {
        var at = forward ? NextTraversable(scope, -1) : PreviousTraversable(scope, _stops.Count);
        return at >= 0 ? new FocusMove(_stops[at].Position, false) : new FocusMove(_primary, false);
    }

    /// <summary>The chain the focus stands under at <paramref name="position"/>: a stop's, the modal scope's when the
    /// position is the layer's own, none for a place the frame does not know.</summary>
    private IReadOnlyList<FocusAttachment> ChainOf(object? position)
    {
        if (position == null) return _none;
        var at = IndexOfStop(position);
        if (at >= 0) return _stops[at].Chain;
        if (_modal != null && IsModalPosition(position))
        {
            var chain = new List<FocusAttachment>();
            chain.Add(new FocusAttachment(_modal, null, false));
            return chain;
        }
        return _none;
    }

    private bool IsModalPosition(object? position) => position != null && _modalPosition != null && ReferenceEquals(position, _modalPosition);

    /// <summary>Where <paramref name="position"/> is among the frame's stops, or -1.</summary>
    private int IndexOfStop(object? position)
    {
        if (position == null) return -1;
        for (var i = 0; i < _stops.Count; i++)
            if (ReferenceEquals(_stops[i].Position, position)) return i;
        return -1;
    }

    /// <summary>Whether the stop at <paramref name="stop"/> stands under <paramref name="scope"/>; every stop is
    /// under no scope.</summary>
    private bool Under(int stop, FocusScopeNode? scope) => scope == null || Contains(_stops[stop].Chain, scope);

    /// <summary>Whether the stop stands under the modal scope, which every stop does when there is none.</summary>
    private bool InModal(int stop) => _modal == null || Contains(_stops[stop].Chain, _modal);

    /// <summary>Whether Tab reaches the stop: its owner may take the focus and does not skip traversal.</summary>
    private bool Traversable(int stop)
    {
        var owner = OwnerOf(_stops[stop].Chain);
        return owner == null || (owner.CanRequestFocus && !owner.SkipTraversal);
    }

    /// <summary>Whether a request may land on the stop: its owner may take the focus, skipping traversal or not.</summary>
    private bool Requestable(int stop)
    {
        var owner = OwnerOf(_stops[stop].Chain);
        return owner == null || owner.CanRequestFocus;
    }

    private int NextTraversable(FocusScopeNode? scope, int after)
    {
        for (var i = after + 1; i < _stops.Count; i++)
            if (Under(i, scope) && Traversable(i)) return i;
        return -1;
    }

    private int PreviousTraversable(FocusScopeNode? scope, int before)
    {
        for (var i = before - 1; i >= 0; i--)
            if (Under(i, scope) && Traversable(i)) return i;
        return -1;
    }

    /// <summary>The last stop under <paramref name="scope"/>, traversable or not: where the scope ends in the frame's order.</summary>
    private int LastUnder(FocusScopeNode? scope)
    {
        for (var i = _stops.Count - 1; i >= 0; i--)
            if (Under(i, scope)) return i;
        return -1;
    }

    /// <summary>The first stop under <paramref name="scope"/>, traversable or not: where the scope begins.</summary>
    private int FirstUnder(FocusScopeNode? scope)
    {
        for (var i = 0; i < _stops.Count; i++)
            if (Under(i, scope)) return i;
        return -1;
    }

    /// <summary>Whether two chains name the same nodes in the same order.</summary>
    private static bool SameNodes(IReadOnlyList<FocusAttachment> a, IReadOnlyList<FocusAttachment> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!ReferenceEquals(a[i].Node, b[i].Node)) return false;
        return true;
    }

    private static bool Contains(IReadOnlyList<FocusAttachment> chain, FocusNode? node)
    {
        for (var i = 0; i < chain.Count; i++)
            if (ReferenceEquals(chain[i].Node, node)) return true;
        return false;
    }

    /// <summary>The node that owns a stop: the innermost <c>Focus</c> above it, a scope being no owner of its stops.</summary>
    private static FocusNode? OwnerOf(IReadOnlyList<FocusAttachment> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
            if (chain[i].Node is not FocusScopeNode) return chain[i].Node;
        return null;
    }

    /// <summary>The innermost scope a stop stands under, or null for the root.</summary>
    private static FocusScopeNode? InnermostScope(IReadOnlyList<FocusAttachment> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
            if (chain[i].Node is FocusScopeNode scope) return scope;
        return null;
    }

    /// <summary>The scope above <paramref name="scope"/> in a chain, or null for the root.</summary>
    private static FocusScopeNode? ScopeOutside(IReadOnlyList<FocusAttachment> chain, FocusScopeNode scope)
    {
        var at = chain.Count - 1;
        while (at >= 0 && !ReferenceEquals(chain[at].Node, scope)) at--;
        for (var i = at - 1; i >= 0; i--)
            if (chain[i].Node is FocusScopeNode outer) return outer;
        return null;
    }
}
