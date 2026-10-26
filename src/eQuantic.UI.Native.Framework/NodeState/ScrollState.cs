namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a <see cref="Primitives.ScrollView"/>'s position keeps: where the host scrolled it, and where
/// a wheel or a fling sent it while it glides there. Moved through the host's
/// <see cref="ScrollClock"/>; read off the position by the layout and the paint walk.
/// </summary>
internal sealed class ScrollState : IDisposable
{
    /// <summary>The offset the host scrolled to, or null while nothing has: the node's own
    /// <c>Offset</c> stands until then.</summary>
    public float? Offset;

    /// <summary>Where the view glides to, or null when it does not.</summary>
    public float? Target;

    /// <summary>The version of the last <see cref="Primitives.ScrollRequest"/> the view went to, or null
    /// while it has honoured none: the same request built again moves nothing.</summary>
    public int? Requested;

    /// <summary>Whether the clock holds it among the gliding views, so it is listed once.</summary>
    public bool Listed;

    /// <summary>Whether its position left the tree: a glide in flight ends there, where the path's
    /// would have gone on moving whatever node took the path next.</summary>
    public bool Released;

    /// <summary>The position left the tree.</summary>
    public void Dispose() => Released = true;
}
