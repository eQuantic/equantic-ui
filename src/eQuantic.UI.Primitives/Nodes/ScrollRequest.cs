namespace eQuantic.UI.Primitives;

/// <summary>
/// Where a <see cref="ScrollView"/> is asked to go: an <see cref="Offset"/> on its axis, clamped to what
/// its content can scroll. A tree is built again and again, so the request carries a
/// <see cref="Version"/>, and the view honours each version once: a request still in the tree after the
/// reader scrolled away moves nothing, and a new version moves the view again.
/// <para>
/// <em>How does Flutter solve it?</em> <c>ScrollController.jumpTo</c>: a component that owns a scroll
/// view tells it where to go. Here the telling is data in the tree, as everything a component says to a
/// realizer is, and the version is what makes data an event. <see cref="TableScrollRequest"/> is the
/// table's, and the precedent.
/// </para>
/// </summary>
public sealed record ScrollRequest(int Version)
{
    /// <summary>The offset to go to, in dp from the content's start.</summary>
    public float Offset { get; init; }
}
