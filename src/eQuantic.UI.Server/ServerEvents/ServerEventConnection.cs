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
/// </summary>
internal sealed class ServerEventConnection
{
    private readonly Channel<string> _frames;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string?>> _topics = new(StringComparer.Ordinal);

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

    public int TopicCount => _topics.Count;

    public IReadOnlyCollection<string> Topics => _topics.Keys.ToList();

    /// <summary>Set by the registry when the stream ends, under its lock: a bind that was still being
    /// authorized finds it set, and binds nothing no one would ever release.</summary>
    public bool Retired { get; set; }

    public bool TryBind(string topic, IReadOnlyDictionary<string, string?> values) => _topics.TryAdd(topic, values);

    public bool Holds(string topic) => _topics.ContainsKey(topic);

    public bool TryRelease(string topic, out IReadOnlyDictionary<string, string?>? values) => _topics.TryRemove(topic, out values);

    /// <summary>Queues a frame; a full queue closes the connection.</summary>
    public void Send(string frame)
    {
        if (!_frames.Writer.TryWrite(frame)) Close();
    }

    public void Close() => _frames.Writer.TryComplete();
}
