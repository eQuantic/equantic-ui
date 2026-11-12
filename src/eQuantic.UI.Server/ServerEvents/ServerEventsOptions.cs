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

    /// <summary>How many streams this instance holds open at once; a page that asks for one more is
    /// answered 503 and tries again later, as it does after any dropped stream (#1292). 10 000 by
    /// default. A cap per client is the app's to set, with .NET's rate limiter on the endpoints
    /// (<see cref="ServerEventsBuilder.ConfigureEndpoints"/>): keyed on the address it would refuse every
    /// page behind one proxy.</summary>
    public int MaxConnections { get; set; } = 10_000;

    /// <summary>How long a stream lives before the server ends it. The page connects again at once and
    /// binds its topics again, each authorized as the request that binds it is then, so a topic bound
    /// before a sign-out or a role the app took away is heard no longer than this (#1292). Each stream
    /// ends up to a tenth earlier, at random, so streams opened together do not end together. Thirty
    /// minutes by default.</summary>
    public TimeSpan MaxConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How many events may wait for a connection that is not reading them. When the queue is
    /// full the connection is closed, and the page connects again and catches up, instead of the
    /// server's memory growing with a slow reader. 256 by default.</summary>
    public int MaxQueuedEventsPerConnection { get; set; } = 256;
}
