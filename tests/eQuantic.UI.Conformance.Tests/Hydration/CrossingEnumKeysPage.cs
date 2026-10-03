using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills dictionaries keyed by an enum and by a flags enum. EqJson refused the
/// first both ways and wrote the second's keys by their names, which the browser does not hold (#442).
/// </summary>
[Page("/crossing-enum-keys")]
public sealed class CrossingEnumKeysPage : StatefulComponent, IServerPrefetch
{
    private readonly Dictionary<CrossingRank, int> _byRank = new();
    private readonly Dictionary<CrossingAccess, int> _byAccess = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _byRank[CrossingRank.Mid] = 5;
        _byRank[CrossingRank.Zeta] = 1;
        _byAccess[CrossingAccess.Read] = 1;
        _byAccess[CrossingAccess.Read | CrossingAccess.Write] = 3;
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var ranks = "";
        foreach (var pair in _byRank) ranks += "," + pair.Key + "=" + pair.Value;

        var page = new Column();
        page.Add(new Text($"ranks {ranks}", TypeRole.BodyM));
        page.Add(new Text($"rank {_byRank.ContainsKey(CrossingRank.Mid)} {_byRank[CrossingRank.Mid]}", TypeRole.BodyM));
        page.Add(new Text($"access {_byAccess[CrossingAccess.Read | CrossingAccess.Write]} {_byAccess.ContainsKey(CrossingAccess.Read)} {_byAccess.Count}", TypeRole.BodyM));
        return page;
    }
}
