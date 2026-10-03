using System;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A property that halves what it is given, keeping its store through C#'s <c>field</c>: hydrated
/// through its setter, the 42 its getter answered on the server would be halved again in the browser.
/// </summary>
[Page("/crossing-halves")]
public sealed class CrossingHalvesPage : StatelessComponent, IServerPrefetch
{
    public int Half { get; set => field = value / 2; }

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Half = 84;
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context) => new Text($"half {Half}", TypeRole.BodyM);
}
