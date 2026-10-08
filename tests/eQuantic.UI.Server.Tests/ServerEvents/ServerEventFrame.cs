namespace eQuantic.UI.Server.Tests.ServerEvents;

/// <summary>One frame read off the event stream: an event with its type, data and id, or a comment
/// line (the heartbeat).</summary>
internal sealed record ServerEventFrame(string? Event, string? Data, string? Id, string? Comment)
{
    public bool IsHeartbeat => Comment is not null;
}
