using System;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills an auto-property, composing a child that prefetches into the
/// primary-constructor parameter it captured: the two shapes whose values drew on the server and
/// vanished on hydration (#509, #510). <see cref="HydrationCrossingTests"/> renders it on the server and
/// runs its twin, transpiled from this file, on the payload the server wrote.
/// </summary>
[Page("/crossing")]
public sealed class CrossingPage : StatelessComponent, IServerPrefetch
{
    public long Downloads { get; private set; }

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Downloads = 42;
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var page = new Column();
        page.Add(new Text($"downloads {Downloads}", TypeRole.BodyM));
        page.Add(new CrossingQuote("acme"));
        return page;
    }
}
