namespace eQuantic.UI.Server;

/// <summary>How one event is published.</summary>
public sealed record ServerEventPublishOptions
{
    /// <summary>
    /// The event's id, sent as the stream's own event id. The page reports the last id it received
    /// when it comes back from a dropped connection (<c>ServerConnection.LastEventId</c>), so the app
    /// can send what was missed since: a sequence number the app already keeps is the usual choice.
    /// </summary>
    public string? Id { get; init; }
}
