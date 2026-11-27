namespace eQuantic.UI.Primitives;

/// <summary>A node whose focus changed in one report, waiting for its handlers to hear it (#1897): the node, the
/// <see cref="FocusNode.HasFocus"/> it settled on, and whether that is a change of it (Flutter calls
/// <c>onFocusChange</c> only then, and a node's listeners when either flag moved).</summary>
internal sealed class FocusEvent
{
    public FocusEvent(FocusNode node, bool hasFocus, bool focusChanged)
    {
        Node = node;
        HasFocus = hasFocus;
        FocusChanged = focusChanged;
    }

    public FocusNode Node { get; }

    public bool HasFocus { get; }

    public bool FocusChanged { get; }
}
