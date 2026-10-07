namespace eQuantic.UI.Server;

/// <summary>
/// One published event as a backplane carries it between server instances: the topic's name, the
/// payload already written as JSON, and the event's id, if any. A backplane moves it as it is.
/// </summary>
/// <param name="Topic">The topic's name.</param>
/// <param name="Payload">The payload, written as JSON in the wire's format.</param>
/// <param name="Id">The event's id, or null.</param>
public sealed record ServerEventEnvelope(string Topic, string Payload, string? Id);
