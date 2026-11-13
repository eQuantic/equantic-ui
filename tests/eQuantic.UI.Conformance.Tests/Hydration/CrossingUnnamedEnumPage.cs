using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills a field, a dictionary's keys and a list with values of an enum that is not a
/// flags one and that no member names, and a field of one over a long past 2^53. Each crossed as its digits
/// in a string, where the browser holds a number or a BigInt, so `v == (CrossingRank)7` compared `"7"` with
/// `7` and answered false (#1238).
/// </summary>
[Page("/crossing-unnamed-enum")]
public sealed class CrossingUnnamedEnumPage : StatefulComponent, IServerPrefetch
{
    private CrossingRank _rank;
    private CrossingRank _named;
    private CrossingTier _tier;
    private readonly Dictionary<CrossingRank, int> _byRank = new();
    private readonly List<CrossingRank> _ranks = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _rank = (CrossingRank)7;
        _named = CrossingRank.Mid;
        _tier = (CrossingTier)4611686018427387905;
        _byRank[(CrossingRank)9] = 4;
        _byRank[CrossingRank.Alpha] = 6;
        _ranks.Add((CrossingRank)8);
        _ranks.Add(CrossingRank.Zeta);
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var page = new Column();
        page.Add(new Text($"rank {_rank == (CrossingRank)7} {(int)_rank + 1} {_named == CrossingRank.Mid}", TypeRole.BodyM));
        page.Add(new Text($"tier {_tier == (CrossingTier)4611686018427387905} {(long)_tier}", TypeRole.BodyM));
        page.Add(new Text($"keys {_byRank.ContainsKey((CrossingRank)9)} {_byRank[(CrossingRank)9]} {_byRank[CrossingRank.Alpha]} {_byRank.Count}", TypeRole.BodyM));
        page.Add(new Text($"list {_ranks[0] == (CrossingRank)8} {(int)_ranks[0]} {_ranks[1] == CrossingRank.Zeta}", TypeRole.BodyM));
        return page;
    }
}
