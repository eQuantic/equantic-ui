using System.Collections.Concurrent;
using System.Threading.Channels;

namespace eQuantic.UI.Server;

/// <summary>
/// One page's open stream: the topics bound to it, each with the values its template matched, and
/// the frames waiting to be written to it. No request's <c>HttpContext</c> is kept: the server reuses
/// them once a request ends, so whoever hears a topic released is handed the request at hand.
/// <para>
/// The queue is bounded: a page that stops reading fills it, and the connection is closed rather than
/// the server's memory growing, so the page connects again and catches up.
/// </para>
/// <para>
/// Its two disposables are never disposed: neither owns a timer or a wait handle, and nothing but this
/// connection holds them, so a late delivery to a closed connection never meets a disposed one.
/// </para>
/// </summary>
internal sealed class ServerEventConnection
{
    private readonly Channel<string> _frames;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string?>> _topics = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _overflowed = new();

    public ServerEventConnection(string id, int capacity)
    {
        Id = id;
        _frames = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public string Id { get; }

    public ChannelReader<string> Frames => _frames.Reader;

    /// <summary>
    /// Cancelled when the queue overflows. The page that filled it is not reading, so the stream is
    /// blocked writing to it: closing the queue alone was seen only at the stream's next read, and the
    /// connection kept its topics, and its presence, until the reader resumed.
    /// </summary>
    public CancellationToken Overflowed => _overflowed.Token;

    /// <summary>
    /// One transition at a time, each with what the handlers hear of it: a bind and its subscribed, a
    /// release and its released, the close and everything it released. A handler still awaiting the
    /// join of a topic was overtaken by the close of its stream, and heard the leave first.
    /// </summary>
    public SemaphoreSlim Transitions { get; } = new(1, 1);

    public int TopicCount => _topics.Count;

    public IReadOnlyCollection<string> Topics => _topics.Keys.ToList();

    /// <summary>Set by the registry when the stream ends, under its lock: a bind that was still being
    /// authorized finds it set, and binds nothing no one would ever release.</summary>
    public bool Retired { get; set; }

    public bool TryBind(string topic, IReadOnlyDictionary<string, string?> values) => _topics.TryAdd(topic, values);

    public bool Holds(string topic) => _topics.ContainsKey(topic);

    public bool TryRelease(string topic, out IReadOnlyDictionary<string, string?>? values) => _topics.TryRemove(topic, out values);

    /// <summary>Queues a frame. A full queue closes the connection and cancels the stream's write.</summary>
    public void Send(string frame)
    {
        if (_frames.Writer.TryWrite(frame)) return;
        // Only an overflow of an open queue: a delivery that reaches a connection its stream has
        // already closed finds the queue complete, and has nothing to cancel.
        if (_frames.Writer.TryComplete()) _overflowed.Cancel();
    }

    public void Close() => _frames.Writer.TryComplete();
}
