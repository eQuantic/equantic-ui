namespace eQuantic.UI.Native.Engine;

/// <summary>
/// The two arrays a live source writes its frames into, turn about, so that the frame a renderer is
/// reading is never the one being written.
/// <para>
/// A camera converts a frame on its own thread, and the renderer uploads the published one on
/// another, with no lock between them: a lock there would hold the render thread for as long as a
/// conversion takes. With ONE array the two overlap, and an upload that lands in the middle of a
/// conversion draws the top of one frame over the bottom of another. With two, the source writes
/// the array nobody was handed, and hands it over only when it is whole.
/// </para>
/// <para>
/// The array handed over stays untouched until ANOTHER frame has been published after it, which is
/// a whole frame of the source later. A realizer keeps one texture per array, so a source that
/// alternates costs two textures and never one per frame.
/// </para>
/// </summary>
public sealed class FramePair
{
    private volatile byte[]? _published;
    private byte[]? _next;

    /// <summary>The frame last published, whole, or null before the first.</summary>
    public byte[]? Published => _published;

    /// <summary>
    /// The array to write the next frame into: never the published one. It is the array published
    /// before that one when its length is the length asked for, and a new array otherwise (the
    /// first frames, and a frame whose size changed).
    /// </summary>
    public byte[] Next(int length)
    {
        if (_next is null || _next.Length != length) _next = new byte[length];
        return _next;
    }

    /// <summary>Hands over the array <see cref="Next"/> answered, now that it holds a whole frame.
    /// The frame published until now becomes the one to write next.</summary>
    public void Publish()
    {
        if (_next is not { } written)
            throw new InvalidOperationException("There is no frame to publish: Next was not asked for one.");
        _next = _published;
        _published = written;
    }
}
