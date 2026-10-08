namespace eQuantic.UI.Server;

/// <summary>
/// Carries published events between the server instances that serve one app, so a page connected to
/// one instance hears what another publishes. The default keeps them in the process, which is all a
/// single instance needs; an app running several registers its own with
/// <c>UseServerEvents(events =&gt; events.UseBackplane&lt;T&gt;())</c>, over Redis pub/sub or a service
/// bus.
/// </summary>
public interface IServerEventBackplane
{
    /// <summary>Hands <paramref name="envelope"/> to every instance's receiver, this one's included.</summary>
    ValueTask PublishAsync(ServerEventEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts delivering every envelope any instance publishes to <paramref name="receive"/>, until the
    /// returned value is disposed. Each instance subscribes once, when it starts.
    /// </summary>
    ValueTask<IAsyncDisposable> SubscribeAsync(Func<ServerEventEnvelope, ValueTask> receive,
        CancellationToken cancellationToken = default);
}
