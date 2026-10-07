using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// Publishes to a <see cref="ServerTopic{T}"/>: every page subscribed to the topic, on every server
/// instance the backplane reaches, receives the payload. Injected by any server code, from a Server
/// Action, a background service, a message handler. Registered by <c>UseServerEvents</c>.
/// </summary>
public interface IServerEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="payload"/> to <paramref name="topic"/>. The payload is written as a
    /// Server Action's result is, and revived as a <typeparamref name="T"/> in the browser. A page that
    /// does not hold the topic when it is published does not receive it: delivery is at most once.
    /// </summary>
    /// <exception cref="InvalidOperationException">The written payload is larger than the configured
    /// <see cref="ServerEventsOptions.MaxPayloadBytes"/>.</exception>
    /// <exception cref="ArgumentException">The options' id holds a line break, which the stream cannot
    /// carry.</exception>
    ValueTask PublishAsync<T>(ServerTopic<T> topic, T payload, ServerEventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
