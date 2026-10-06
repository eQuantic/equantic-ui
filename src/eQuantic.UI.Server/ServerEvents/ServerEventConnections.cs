using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// This instance's open connections, and which of them hold each topic: where an envelope the
/// backplane delivers is written. A connection's id is a random token, handed only to the stream that
/// opened it, so a request that names it is the page that holds it.
/// </summary>
internal sealed class ServerEventConnections
{
    private readonly ConcurrentDictionary<string, ServerEventConnection> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ServerEventConnection>> _byTopic = new(StringComparer.Ordinal);
    private readonly Lock _index = new();

    public ServerEventConnection Open(int capacity)
    {
        var connection = new ServerEventConnection(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)), capacity);
        _connections[connection.Id] = connection;
        return connection;
    }

    public bool TryGet(string id, out ServerEventConnection? connection) => _connections.TryGetValue(id, out connection);

    /// <summary>
    /// Binds the topic to the connection, unless the connection already holds it, holds as many as
    /// <paramref name="limit"/> allows, or closed while the request was being authorized. Decided
    /// under the lock <see cref="Close"/> takes, so two requests at once cannot pass the limit, and a
    /// topic is never bound to a stream that has already released everything it held.
    /// </summary>
    public ServerTopicBinding Bind(ServerEventConnection connection, ServerTopicContext topic, int limit)
    {
        lock (_index)
        {
            if (connection.Retired) return ServerTopicBinding.ConnectionClosed;
            if (connection.Holds(topic.Topic)) return ServerTopicBinding.AlreadyHeld;
            if (connection.TopicCount >= limit) return ServerTopicBinding.LimitReached;
            connection.TryBind(topic.Topic, topic.Values);
            if (!_byTopic.TryGetValue(topic.Topic, out var holders))
                _byTopic[topic.Topic] = holders = new Dictionary<string, ServerEventConnection>(StringComparer.Ordinal);
            holders[connection.Id] = connection;
            return ServerTopicBinding.Bound;
        }
    }

    /// <summary>Releases the topic from the connection, answering it as the request at hand meets it,
    /// or null when the connection did not hold it.</summary>
    public ServerTopicContext? Release(ServerEventConnection connection, string topic, HttpContext httpContext)
    {
        lock (_index)
        {
            if (!connection.TryRelease(topic, out var values)) return null;
            if (_byTopic.TryGetValue(topic, out var holders))
            {
                holders.Remove(connection.Id);
                if (holders.Count == 0) _byTopic.Remove(topic);
            }
            return new ServerTopicContext(connection.Id, topic, values!, httpContext);
        }
    }

    /// <summary>Closes the connection, answering the topics it held, each released. Nothing binds to it
    /// afterwards (<see cref="Bind"/>).</summary>
    public IReadOnlyList<ServerTopicContext> Close(ServerEventConnection connection, HttpContext httpContext)
    {
        _connections.TryRemove(connection.Id, out _);
        var released = new List<ServerTopicContext>();
        lock (_index)
        {
            connection.Retired = true;
            foreach (var topic in connection.Topics)
            {
                if (Release(connection, topic, httpContext) is { } context) released.Add(context);
            }
        }
        connection.Close();
        return released;
    }

    /// <summary>Writes an envelope to every connection here that holds its topic.</summary>
    public ValueTask DeliverAsync(ServerEventEnvelope envelope)
    {
        ServerEventConnection[] holders;
        lock (_index)
        {
            if (!_byTopic.TryGetValue(envelope.Topic, out var found)) return ValueTask.CompletedTask;
            holders = [.. found.Values];
        }

        var frame = ServerEventFrames.Message(envelope);
        foreach (var connection in holders) connection.Send(frame);
        return ValueTask.CompletedTask;
    }
}
