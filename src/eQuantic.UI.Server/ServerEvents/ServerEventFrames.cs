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

    /// <summary>Whether an event id can stand on a frame's <c>id:</c> line: no line break, and no NUL,
    /// which a browser's <c>EventSource</c> drops the whole field for.</summary>
    public static bool IsWritableId(string? id) => id is null || id.AsSpan().IndexOfAny('\r', '\n', '\0') < 0;

    /// <summary>
    /// Whether an envelope can be written as a frame: its id one a line can carry, and its payload one
    /// JSON value, as the publisher writes it. Anything else came from a writer that is not this app's
    /// publisher, and could add fields of its own to the frame or to the message the page reads (#1292).
    /// </summary>
    public static bool CanCarry(ServerEventEnvelope envelope)
    {
        if (!IsWritableId(envelope.Id)) return false;
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(envelope.Payload));
            reader.Read();
            reader.Skip();
            return !reader.Read();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string Connected(string connectionId) =>
        $"event: connection\ndata: {{\"id\":{JsonSerializer.Serialize(connectionId)}}}\n\n";

    public static string Message(ServerEventEnvelope envelope)
    {
        var frame = new StringBuilder();
        if (envelope.Id is not null) frame.Append("id: ").Append(envelope.Id).Append('\n');
        frame.Append("event: message\n");
        // A payload a backplane carried may hold line breaks; each line is a data line of its own,
        // which the browser joins back with a line feed, so the JSON reads as it was written. Only the
        // three breaks the format knows split it (CR, LF, CRLF): U+2028, U+2029 and NEL are characters
        // a JSON string may hold, and split there they would come back as a line feed inside the
        // string, which JSON refuses.
        var data = $"{{\"topic\":{JsonSerializer.Serialize(envelope.Topic)},\"payload\":{envelope.Payload}}}";
        foreach (var line in data.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            frame.Append("data: ").Append(line).Append('\n');
        return frame.Append('\n').ToString();
    }
}
