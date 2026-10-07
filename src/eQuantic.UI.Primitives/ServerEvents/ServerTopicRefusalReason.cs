namespace eQuantic.UI.Primitives;

/// <summary>Why the server did not bind a topic to the page.</summary>
public enum ServerTopicRefusalReason
{
    /// <summary>A template matches the topic, and its authorization refused this request.</summary>
    Forbidden,

    /// <summary>No template the server configures matches the topic.</summary>
    Unknown,

    /// <summary>The connection already holds as many topics as the server allows one to.</summary>
    LimitReached,

    /// <summary>The request did not reach an answer: the server failed, or the network did.</summary>
    Failed,
}
