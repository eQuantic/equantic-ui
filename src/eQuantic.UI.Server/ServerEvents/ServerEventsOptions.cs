namespace eQuantic.UI.Server;

/// <summary>
/// The limits of the pages' connections to the server's events, bound from the
/// <c>EQuantic:ServerEvents</c> section of the app's configuration and overridable in
/// <c>UseServerEvents(events =&gt; events.Configure(…))</c>.
/// </summary>
public sealed class ServerEventsOptions
{
    /// <summary>The configuration section the options are bound from.</summary>
    public const string SectionName = "EQuantic:ServerEvents";

    /// <summary>How long a connection may go without sending anything before it sends a heartbeat,
    /// which keeps proxies from closing an idle stream. Fifteen seconds by default.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>How many topics one connection may hold; a subscription past it is refused. 64 by
    /// default.</summary>
    public int MaxTopicsPerConnection { get; set; } = 64;

    /// <summary>The largest payload, written as JSON, in bytes; publishing a larger one throws. 64 KiB
    /// by default.</summary>
    public int MaxPayloadBytes { get; set; } = 64 * 1024;

    /// <summary>How many events may wait for a connection that is not reading them. When the queue is
    /// full the connection is closed, and the page connects again and catches up, instead of the
    /// server's memory growing with a slow reader. 256 by default.</summary>
    public int MaxQueuedEventsPerConnection { get; set; } = 256;
}
