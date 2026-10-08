using System.Text;
using System.Text.Json;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Json;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Writes a payload as the wire writes a Server Action's result (<see cref="EqJson"/>: a long and a
/// decimal as text, so the browser revives them exactly) and hands it to the backplane. A payload
/// past the configured size throws here, where the publisher can see it, instead of being dropped on
/// the way.
/// </summary>
internal sealed class ServerEventPublisher(IServerEventBackplane backplane, IOptions<ServerEventsOptions> options)
    : IServerEventPublisher
{
    public ValueTask PublishAsync<T>(ServerTopic<T> topic, T payload, ServerEventPublishOptions? publish = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        var id = publish?.Id;
        if (id is not null && id.AsSpan().IndexOfAny('\r', '\n', '\0') >= 0)
            throw new ArgumentException(
                $"The id of an event published to '{topic.Name}' holds a line break, which the event stream cannot carry.",
                nameof(publish));

        var json = JsonSerializer.Serialize(payload, EqJson.Options);
        var limit = options.Value.MaxPayloadBytes;
        var size = Encoding.UTF8.GetByteCount(json);
        if (size > limit)
            throw new InvalidOperationException(
                $"The payload published to '{topic.Name}' is {size} bytes written, past the {limit} bytes "
                + $"{ServerEventsOptions.SectionName}:{nameof(ServerEventsOptions.MaxPayloadBytes)} allows.");

        return backplane.PublishAsync(new ServerEventEnvelope(topic.Name, json, id), cancellationToken);
    }
}
