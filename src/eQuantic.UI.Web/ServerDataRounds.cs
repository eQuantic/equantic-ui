using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The rounds that load a page's server data through a <see cref="MarkupHost"/>. After each frame,
/// lowered with a <see cref="ComponentExpansionScope"/> naming its components, <see cref="Next"/> says
/// which components that load data (<see cref="IServerPrefetch"/>) are asked now; once one has loaded,
/// <see cref="Loaded"/> marks its positions, so the next frame builds those subtrees alone, with the
/// instance that loaded (docs/ELEMENT-TREE-PLAN.md 3.7). Nothing is written back onto a fresh instance:
/// the one that loaded is the one drawn.
/// <para>
/// The server and the component tester run the same rounds. What each does when a page never settles
/// is its own: the server draws what it has and says so in its log, a test fails.
/// </para>
/// </summary>
public sealed class ServerDataRounds
{
    private readonly MarkupHost _host;
    private readonly HashSet<UiComponent> _asked = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ComponentElement> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<UiComponent, List<ComponentElement>> _positions = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<IServerPrefetch> _pending = [];

    /// <summary>The rounds of the page <paramref name="host"/> draws.</summary>
    public ServerDataRounds(MarkupHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Every component of the last frame that loads data and has not been asked, the ones
    /// <see cref="Next"/> answered included. Empty when the page has settled.</summary>
    public IReadOnlyList<IServerPrefetch> Pending => _pending;

    /// <summary>
    /// Whether the last frame reached something to ask that no frame before it had: a position, or a
    /// component that holds none, which a tree drawn once makes anew each frame. What a bound on the
    /// rounds counts, so a component waiting its turn below another spends none, and a chain inside an
    /// arm drawn once cannot go on for ever.
    /// </summary>
    public bool ReachedNew { get; private set; }

    /// <summary>
    /// The components to ask now, after a frame lowered with <paramref name="scope"/> armed: each that
    /// loads data, that the frame reached and that has not been asked, in the order the scope named
    /// them. None when the page has settled.
    /// <para>
    /// A STATELESS component below another asked in the same round waits for the next one: that one's
    /// position builds again once it has loaded, and hands this position a fresh instance, since a
    /// stateless one is never kept, so what the old instance loaded would go with it. A stateful one is
    /// kept through its parent's build, and is asked now.
    /// </para>
    /// <para>
    /// A component that holds no position is asked too, with none to mark: an adaptive node's arm the
    /// element does not hold lowers as a tree drawn once, and its instance is the node the arm holds,
    /// the same from one frame to the next while its parent does not build again.
    /// </para>
    /// </summary>
    /// <param name="scope">The scope armed while the frame was lowered.</param>
    public IReadOnlyList<IServerPrefetch> Next(ComponentExpansionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var reached = new List<UiComponent>();
        var reachedSet = new HashSet<UiComponent>(ReferenceEqualityComparer.Instance);
        foreach (var component in scope.Expanded.Values)
        {
            if (component is IServerPrefetch && !_asked.Contains(component) && reachedSet.Add(component))
                reached.Add(component);
        }

        _pending = reached.Cast<IServerPrefetch>().ToList();
        ReachedNew = false;
        if (reached.Count == 0) return [];

        _positions.Clear();
        foreach (var component in reached) _positions[component] = [];
        if (_host.Owner.Root is { } root) Collect(root, _positions);

        foreach (var held in _positions.Values)
        {
            if (held.Count == 0) ReachedNew = true;
            foreach (var position in held) ReachedNew |= _seen.Add(position);
        }

        var due = new List<IServerPrefetch>();
        foreach (var component in reached)
        {
            if (component is not StatefulComponent && _positions[component].Any(position => BelowAnotherAsking(position, reachedSet)))
                continue;
            _asked.Add(component);
            due.Add((IServerPrefetch)component);
        }

        return due;
    }

    /// <summary>
    /// Marks the positions of <paramref name="component"/>, which has just loaded: what it loaded
    /// changed what it builds and nothing said so, so its positions build again in the next frame, with
    /// the instance that loaded, and nothing else does. Building everything instead would hand a
    /// stateless component's position the fresh instance its parent makes, and the data would go.
    /// </summary>
    /// <param name="component">A component <see cref="Next"/> answered.</param>
    public void Loaded(IServerPrefetch component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component is not UiComponent instance || !_positions.TryGetValue(instance, out var held)) return;
        foreach (var position in held) position.MarkNeedsBuild();
    }

    /// <summary>Every position holding one of the components in <paramref name="positions"/>, in tree
    /// order: one, unless a parent placed the same instance twice.</summary>
    private static void Collect(Element element, Dictionary<UiComponent, List<ComponentElement>> positions)
    {
        if (element is ComponentElement position && positions.TryGetValue(position.Component, out var held))
            held.Add(position);

        element.VisitChildren(child => Collect(child, positions));
    }

    /// <summary>Whether a component above <paramref name="position"/> asks for data in this round.</summary>
    private static bool BelowAnotherAsking(Element position, HashSet<UiComponent> asking)
    {
        for (var above = position.Parent; above is not null; above = above.Parent)
        {
            if (above is ComponentElement { Component: var component } && asking.Contains(component)) return true;
        }

        return false;
    }
}
