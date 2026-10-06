using System.Text;
using System.Text.Json;

namespace eQuantic.UI.Server;

/// <summary>
/// The stream's frames, in the Server-Sent Events format the browser's <c>EventSource</c> reads: the
/// connection's id first, then each event as a <c>message</c> with the topic and the payload, and a
/// comment line as the heartbeat.
/// </summary>
internal static class ServerEventFrames
{
    public const string Heartbeat = ": ping\n\n";

    public static string Connected(string connectionId) =>
        $"event: connection\ndata: {{\"id\":{JsonSerializer.Serialize(connectionId)}}}\n\n";

    public static string Message(ServerEventEnvelope envelope)
    {
        var frame = new StringBuilder();
        if (envelope.Id is not null) frame.Append("id: ").Append(envelope.Id).Append('\n');
        frame.Append("event: message\n");
        // A payload a backplane carried may hold line breaks; each line is a data line of its own,
        // which the browser joins back with the same break, so the JSON reads as it was written.
        var data = $"{{\"topic\":{JsonSerializer.Serialize(envelope.Topic)},\"payload\":{envelope.Payload}}}";
        foreach (var line in data.ReplaceLineEndings("\n").Split('\n'))
            frame.Append("data: ").Append(line).Append('\n');
        return frame.Append('\n').ToString();
    }
}
