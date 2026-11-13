using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills dictionaries keyed by a record and by a struct holding a long. The keys arrived as
/// the plain objects JSON parses, which equal no key the browser builds, so the first build drew the other branch
/// of what the server drew (#706).
/// </summary>
[Page("/crossing-record-keys")]
public sealed class CrossingRecordKeysPage : StatefulComponent, IServerPrefetch
{
    private readonly Dictionary<CrossingPoint, int> _byPoint = new();
    private readonly Dictionary<CrossingCell, string> _byCell = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _byPoint[new CrossingPoint(1, 2)] = 5;
        _byCell[new CrossingCell(9007199254740993, 2)] = "far";
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var keys = "";
        foreach (var pair in _byPoint) keys += "," + pair.Key.X + ":" + pair.Key.Y + "=" + pair.Value;

        var page = new Column();
        page.Add(new Text($"keys {keys}", TypeRole.BodyM));
        page.Add(new Text($"point {_byPoint[new CrossingPoint(1, 2)]} {_byPoint.ContainsKey(new CrossingPoint(1, 2))} {_byPoint.ContainsKey(new CrossingPoint(2, 1))}", TypeRole.BodyM));
        page.Add(new Text($"cell {_byCell[new CrossingCell(9007199254740993, 2)]} {_byCell.ContainsKey(new CrossingCell(9007199254740993, 2))}", TypeRole.BodyM));
        return page;
    }
}
