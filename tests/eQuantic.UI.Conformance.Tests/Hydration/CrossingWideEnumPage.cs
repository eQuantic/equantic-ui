using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills a field, a dictionary and a list with a flags enum over a long, and a field
/// with a 32-bit flags enum. The 64-bit values crossed as JSON numbers, which lost their low digits in
/// the browser, and a top-level flags field crossed by its members' names, which no value equals (#551).
/// </summary>
[Page("/crossing-wide-enum")]
public sealed class CrossingWideEnumPage : StatefulComponent, IServerPrefetch
{
    private CrossingReach _reach;
    private CrossingAccess _access;
    private readonly Dictionary<CrossingReach, int> _byReach = new();
    private readonly List<CrossingReach> _reaches = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _reach = CrossingReach.Far | CrossingReach.Near;
        _access = CrossingAccess.Read | CrossingAccess.Write;
        _byReach[CrossingReach.Far] = 7;
        _byReach[CrossingReach.Far | CrossingReach.Near] = 9;
        _reaches.Add(CrossingReach.Far);
        _reaches.Add(CrossingReach.Near);
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var page = new Column();
        page.Add(new Text($"reach {(long)_reach} {_reach}", TypeRole.BodyM));
        page.Add(new Text($"access {(int)_access} {_access}", TypeRole.BodyM));
        page.Add(new Text($"keys {_byReach[CrossingReach.Far]} {_byReach.ContainsKey(CrossingReach.Far | CrossingReach.Near)} {_byReach.Count}", TypeRole.BodyM));
        page.Add(new Text($"list {_reaches[0] == CrossingReach.Far} {(long)_reaches[0]} {_reaches.Count}", TypeRole.BodyM));
        return page;
    }
}
