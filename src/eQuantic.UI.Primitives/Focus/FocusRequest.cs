namespace eQuantic.UI.Primitives;

/// <summary>A node's ask of the <see cref="FocusManager"/> (#1897), kept until the write that follows it, with the
/// order it was made in so that of two asks the later wins, even when one was made before its node was shown.</summary>
internal sealed class FocusRequest
{
    public FocusRequest(FocusRequestKind kind, FocusNode node, int ticket)
    {
        Kind = kind;
        Node = node;
        Ticket = ticket;
    }

    public FocusRequestKind Kind { get; }

    public FocusNode Node { get; }

    /// <summary>The order of the ask among every ask of every node: a bigger one is later.</summary>
    public int Ticket { get; }
}
