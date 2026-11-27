using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

/// <summary>
/// What an app writes around a <see cref="FocusNode"/> (#1897): handlers added to and removed from its
/// <c>Changed</c>, from inside the handlers themselves, and an <c>onFocusChange</c> that says what it heard. It writes
/// down every call: compiled here for <see cref="FocusTraversalFixtureTests"/>, and transpiled by eqc into the
/// runtime's <c>shared/__fixtures__/</c>, where <c>focus-traversal.spec.ts</c> drives its twin through the same
/// cases, so the way an event dispatches while a handler changes it (<c>+=</c> and <c>-=</c> of a C# event) is
/// executed on both sides, and a difference is the transpiler's.
/// </summary>
public sealed class FocusProbe
{
    private readonly string _name;
    private readonly List<string> _heard;
    private readonly FocusNode _node;
    private readonly Dictionary<string, Action> _handlers = new();

    /// <param name="name">How the node signs what it hears.</param>
    /// <param name="heard">Where it writes what it hears.</param>
    /// <param name="node">The node whose <c>Changed</c> the handlers join.</param>
    public FocusProbe(string name, List<string> heard, FocusNode node)
    {
        _name = name;
        _heard = heard;
        _node = node;
        OnFocusChange = hasFocus => _heard.Add($"{_name}.onFocusChange {(hasFocus ? "true" : "false")}");
    }

    /// <summary>The <c>onFocusChange</c> a <c>Focus</c> around the node is built with.</summary>
    public Action<bool> OnFocusChange { get; }

    /// <summary>Writes "name.changed" each time the node changes.</summary>
    public void Watch() => Add($"{_name}.changed");

    /// <summary>
    /// Adds the handler named <paramref name="handler"/> to <c>Changed</c>, the same delegate each time it is added:
    /// it writes its name, and the first time it is called it removes <paramref name="removes"/> and adds
    /// <paramref name="adds"/>, as a handler that unsubscribes itself and its neighbour does.
    /// </summary>
    public void Add(string handler, string? removes = null, string? adds = null)
    {
        if (!_handlers.TryGetValue(handler, out var action))
        {
            var first = true;
            action = () =>
            {
                _heard.Add(handler);
                if (!first) return;
                first = false;
                if (removes != null) Remove(removes);
                if (adds != null) Add(adds);
            };
            _handlers[handler] = action;
        }
        _node.Changed += action;
    }

    /// <summary>Removes the handler named <paramref name="handler"/> once, as <c>-=</c> does.</summary>
    public void Remove(string handler)
    {
        if (_handlers.TryGetValue(handler, out var action)) _node.Changed -= action;
    }
}
