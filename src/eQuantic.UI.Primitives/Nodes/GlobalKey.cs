namespace eQuantic.UI.Primitives;

/// <summary>
/// An identity a node keeps wherever it is placed in the tree (#797). A node that carries one and leaves
/// its position, and is placed under another parent in the same build, takes its position with it: the
/// stateful components below it keep their instances, and what they hold, instead of starting again. A
/// screen in both arms of an <see cref="AdaptiveNode"/>, a form a window crossing a threshold moves from a
/// column into a row, keeps what was typed into it.
/// <para>
/// Flutter's <c>GlobalKey</c>. It is an identity, not a value: the app makes one and keeps it where the
/// tree is built, in a field of the stateful component that builds it, and hands the same instance to the
/// node on every build. Two nodes of one key on screen at once are each a position of their own, as the
/// web's arms are while a page is served with all of them.
/// </para>
/// </summary>
public sealed class GlobalKey
{
    /// <summary>A key named <paramref name="label"/>, for a reader: the identity is the instance.</summary>
    public GlobalKey(string? label = null)
    {
        Label = label;
    }

    /// <summary>What the key is called where it is read: a debugging aid, never compared.</summary>
    public string? Label { get; }

    /// <inheritdoc />
    public override string ToString() => Label is { Length: > 0 } label ? $"GlobalKey({label})" : "GlobalKey";
}
