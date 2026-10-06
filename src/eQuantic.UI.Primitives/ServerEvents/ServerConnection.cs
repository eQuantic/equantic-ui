namespace eQuantic.UI.Primitives;

/// <summary>One reading of the page's connection to the server's events.</summary>
/// <param name="State">Where the connection stands.</param>
/// <param name="LastEventId">The id of the last event the page received, when the server published
/// it with one. A page that comes back from <see cref="ServerConnectionState.Reconnecting"/> asks the
/// server for what it missed since then.</param>
[ZeroConstructs("primitive-values.ts: `constructor(state = 'disconnected', lastEventId = null)`.")]
public readonly record struct ServerConnection(ServerConnectionState State, string? LastEventId)
{
    /// <summary>No connection, and no event received.</summary>
    public static readonly ServerConnection Disconnected = new(ServerConnectionState.Disconnected, null);
}
