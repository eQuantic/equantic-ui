namespace eQuantic.UI.Server;

/// <summary>What became of a topic the registry was asked to bind to a connection.</summary>
internal enum ServerTopicBinding
{
    /// <summary>Bound now: what is published to the topic reaches the connection.</summary>
    Bound,

    /// <summary>The connection already held it.</summary>
    AlreadyHeld,

    /// <summary>The connection holds as many topics as the server allows one to.</summary>
    LimitReached,

    /// <summary>The connection's stream ended before the topic could be bound.</summary>
    ConnectionClosed,
}
