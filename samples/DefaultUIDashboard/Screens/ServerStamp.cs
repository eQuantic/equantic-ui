using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A child whose prefetch rewrites the parameter its parent passed. The browser builds it with the
/// same argument, so what it draws after hydration is the server's text only if the value crossed.
/// </summary>
public sealed class ServerStamp(string label) : StatelessComponent, IServerPrefetch
{
    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        label = $"{label} on the server at {DateTime.UtcNow:HH:mm:ss} UTC";
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context) =>
        Text(label, TypeRole.BodyM, context.Theme.TextSecondary);
}
