using System;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A child whose prefetch rewrites the primary-constructor parameter it captured, so the value its
/// twin must draw is the server's, not the argument its parent passes on both sides.
/// </summary>
public sealed class CrossingQuote(string symbol) : StatelessComponent, IServerPrefetch
{
    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        symbol = symbol.ToUpperInvariant();
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context) => new Text($"quote {symbol}", TypeRole.BodyM);
}
