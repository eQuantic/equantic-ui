using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills a dictionary keyed by integers out of order, with a slot a removal freed
/// taken again, and one keyed by text that looks like integers. A JSON object lists such keys first and
/// ascending, so the browser's first build enumerated them in another order than the server drew (#437).
/// </summary>
[Page("/crossing-key-order")]
public sealed class CrossingKeyOrderPage : StatefulComponent, IServerPrefetch
{
    private readonly Dictionary<int, string> _scores = new();
    private readonly Dictionary<string, int> _codes = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _scores[3] = "c";
        _scores[1] = "a";
        _scores[2] = "b";
        _scores.Remove(1);
        _scores[5] = "e";
        _codes["b"] = 1;
        _codes["10"] = 2;
        _codes["9"] = 3;
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var scores = "";
        foreach (var pair in _scores) scores += "," + pair.Key + "=" + pair.Value;
        var codes = "";
        foreach (var pair in _codes) codes += "," + pair.Key + "=" + pair.Value;

        var page = new Column();
        page.Add(new Text($"scores {scores}", TypeRole.BodyM));
        page.Add(new Text($"codes {codes}", TypeRole.BodyM));
        return page;
    }
}
