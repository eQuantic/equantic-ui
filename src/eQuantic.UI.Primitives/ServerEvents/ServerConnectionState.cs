namespace eQuantic.UI.Primitives;

/// <summary>Where the page's connection to the server's events stands.</summary>
public enum ServerConnectionState
{
    /// <summary>No connection, because nothing on the page subscribes to a topic, or because the
    /// target has no server to hear (server rendering).</summary>
    Disconnected,

    /// <summary>Opening the connection for the first subscription.</summary>
    Connecting,

    /// <summary>Open: what the server publishes to a subscribed topic arrives.</summary>
    Connected,

    /// <summary>The connection dropped and is being opened again. Nothing arrives until it is back,
    /// and then every live subscription is made again.</summary>
    Reconnecting,
}
