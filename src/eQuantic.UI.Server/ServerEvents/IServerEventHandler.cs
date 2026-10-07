namespace eQuantic.UI.Server;

/// <summary>
/// Hears the lifecycle of the pages' connections on the server: a connection opened and closed, a
/// topic bound to one and released. Presence, occupancy and audit are the app's own code on top of
/// it. Registered with <c>UseServerEvents(events =&gt; events.AddHandler&lt;T&gt;())</c> and resolved
/// from the request's services. Every member has a default that does nothing, so a handler writes
/// only what it needs. A handler that throws is logged, and the connection carries on.
/// </summary>
public interface IServerEventHandler
{
    /// <summary>A page opened its connection.</summary>
    ValueTask OnConnectedAsync(ServerEventConnectionContext connection) => ValueTask.CompletedTask;

    /// <summary>A topic was authorized and bound to a connection.</summary>
    ValueTask OnSubscribedAsync(ServerTopicContext topic) => ValueTask.CompletedTask;

    /// <summary>A topic was released from a connection: by the page, or because the connection closed.</summary>
    ValueTask OnReleasedAsync(ServerTopicContext topic) => ValueTask.CompletedTask;

    /// <summary>A page's connection closed, after each of its topics was released.</summary>
    ValueTask OnDisconnectedAsync(ServerEventConnectionContext connection) => ValueTask.CompletedTask;
}
