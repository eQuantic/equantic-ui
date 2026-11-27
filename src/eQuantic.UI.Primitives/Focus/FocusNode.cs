namespace eQuantic.UI.Primitives;

/// <summary>
/// The focus, as an object a component holds (#1897): Flutter's <c>FocusNode</c>. A component keeps one the way it
/// keeps a controller, hands it to a <c>Focus</c> around any child, and the node stands for that child's keyboard
/// focus on every target: it asks for the focus, gives it up, says whether it has it, and tells when that changes.
/// <para>
/// A node outlives every rebuild of the component that names it, and the nodes a realizer's frame names stand in a
/// tree of their own, which the <see cref="FocusManager"/> makes from what each stop stands under (Flutter's
/// second tree: the element tree makes the focus tree's shape, and a node never holds its parent from the app's
/// side). That tree is internal. <see cref="FocusManager"/> decides where the focus goes; this only asks it.
/// </para>
/// <para>
/// A node owns the stops under its <c>Focus</c> that no <c>Focus</c> inside it owns. <see cref="HasPrimaryFocus"/>
/// is true while one of them holds the keyboard, and <see cref="HasFocus"/> while any stop under the <c>Focus</c>
/// does. In the common case, a <c>Focus</c> around a button, a node has one stop and the answers are Flutter's. A
/// <c>Focus</c> around several stops is a group: Flutter would focus the node itself, which no target can show.
/// </para>
/// </summary>
public class FocusNode
{
    /// <summary>The order of every ask of every node, so that of two the later wins.</summary>
    private static int _asks;

    /// <summary>Raised when <see cref="HasFocus"/> or <see cref="HasPrimaryFocus"/> changed: Flutter's
    /// <c>ChangeNotifier</c>, in the idiom of <c>FormController.Changed</c>. A handler added or removed while it is
    /// raised takes effect from the next change, as a C# event's do.</summary>
    public event Action? Changed;

    /// <summary>Whether the node may take the keyboard: Flutter's <c>canRequestFocus</c>. A node that may not is
    /// never reached by Tab, and <see cref="RequestFocus"/> moves nothing.</summary>
    public bool CanRequestFocus { get; set; } = true;

    /// <summary>Whether Tab goes past the node: Flutter's <c>skipTraversal</c>. <see cref="RequestFocus"/> still
    /// gives it the keyboard.</summary>
    public bool SkipTraversal { get; set; }

    /// <summary>Whether a stop under the node holds the keyboard: Flutter's <c>hasFocus</c>.</summary>
    public bool HasFocus { get; internal set; }

    /// <summary>Whether a stop the node owns holds the keyboard: Flutter's <c>hasPrimaryFocus</c>.</summary>
    public bool HasPrimaryFocus { get; internal set; }

    /// <summary>
    /// Asks for the keyboard on the node's first traversable stop: Flutter's <c>requestFocus</c>. It takes effect
    /// after the next write of the tree, so a control the same state change removes is gone before the keyboard
    /// moves, and of two asks before one write the later wins. An ask made before any <c>Focus</c> has shown the node
    /// is kept and honoured when one first does.
    /// </summary>
    public void RequestFocus() => Ask(FocusRequestKind.Focus);

    /// <summary>Gives the keyboard up when the node has it, after the next write: Flutter's <c>unfocus</c>, whose
    /// default leaves nothing focused.</summary>
    public void Unfocus()
    {
        if (HasFocus) Ask(FocusRequestKind.Unfocus);
    }

    /// <summary>Moves the keyboard to the stop after the one that has it, as Tab does, after the next write:
    /// Flutter's <c>nextFocus</c>. The walk is the manager's, from where the keyboard is, so inside a modal layer it
    /// stays in the layer.</summary>
    /// <returns>Whether the keyboard has somewhere to go: false when the node is not shown, when the walk leaves the
    /// page (the browser's own Tab does that), and when it ends where it began.</returns>
    public bool NextFocus() => Manager is { } manager && manager.AskToMove(this, true);

    /// <summary>Moves the keyboard to the stop before the one that has it, as Shift+Tab does, after the next write:
    /// Flutter's <c>previousFocus</c>.</summary>
    /// <returns>Whether the keyboard has somewhere to go, as <see cref="NextFocus"/> answers.</returns>
    public bool PreviousFocus() => Manager is { } manager && manager.AskToMove(this, false);

    /// <summary>The manager that last showed the node, or null while no frame has.</summary>
    internal FocusManager? Manager { get; set; }

    /// <summary>The node above this one in the tree of the latest frame.</summary>
    internal FocusNode? Parent { get; set; }

    /// <summary>The nodes directly under this one in the tree of the latest frame.</summary>
    internal List<FocusNode> Children { get; } = new();

    /// <summary>The nearest scope above this node in the tree of the latest frame: Flutter's <c>nearestScope</c>.</summary>
    internal FocusScopeNode? EnclosingScope { get; set; }

    /// <summary>The frame of the manager that last found this node, which tells a node seen already.</summary>
    internal int Frame { get; set; }

    /// <summary>The order of an ask made while no manager had the node, or 0 for none.</summary>
    internal int PendingAsk { get; set; }

    /// <summary>The <c>onFocusChange</c> the latest build wrote for the node.</summary>
    internal Action<bool>? OnFocusChange { get; set; }

    /// <summary>Raises <see cref="Changed"/>: the handlers subscribed when it began are the ones called.</summary>
    internal void RaiseChanged() => Changed?.Invoke();

    /// <summary>The order of the next ask, after every ask made so far by any node.</summary>
    internal static int NextAsk()
    {
        _asks++;
        return _asks;
    }

    private void Ask(FocusRequestKind kind)
    {
        var ask = NextAsk();
        if (Manager is { } manager) manager.Ask(new FocusRequest(kind, this, ask));
        else PendingAsk = ask;
    }
}
