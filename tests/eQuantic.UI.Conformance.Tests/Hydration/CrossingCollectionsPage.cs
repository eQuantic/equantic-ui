using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills the collections the browser holds as its own classes: a set, a stack,
/// a queue of longs, a linked list and a sorted set. Each crossed as the array the server writes, into
/// code that reads a Set or the runtime's class (#516). The sorted ones of strings and decimals, and the
/// sorted dictionary, enumerate in their element type's order, which a rebuild by <c>&lt;</c> lost.
/// </summary>
[Page("/crossing-collections")]
public sealed class CrossingCollectionsPage : StatefulComponent, IServerPrefetch
{
    private readonly HashSet<string> _roles = new();
    private readonly Stack<int> _stack = new();
    private readonly Queue<long> _queue = new();
    private readonly LinkedList<string> _list = new();
    private readonly SortedSet<int> _sorted = new();
    private readonly SortedSet<string> _names = new();
    private readonly SortedSet<decimal> _prices = new();
    private readonly SortedDictionary<string, int> _index = new();
    private readonly SortedSet<CrossingRank> _ranks = new();
    private readonly Queue<decimal> _amounts = new();

    [ServerOnly]
    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _roles.Add("admin");
        _roles.Add("editor");
        _stack.Push(1);
        _stack.Push(2);
        _stack.Push(3);
        _queue.Enqueue(9007199254740993L);
        _queue.Enqueue(2);
        _list.AddLast("a");
        _list.AddLast("b");
        _sorted.Add(3);
        _sorted.Add(1);
        _sorted.Add(2);
        foreach (var name in new[] { "b", "B", "a" }) _names.Add(name);
        foreach (var price in new[] { 10m, 9m, 1.0m, 1.00m }) _prices.Add(price);
        _index["b"] = 1;
        _index["B"] = 2;
        _index["a"] = 3;
        foreach (var rank in new[] { CrossingRank.Mid, CrossingRank.Alpha, CrossingRank.Zeta }) _ranks.Add(rank);
        _amounts.Enqueue(1m);
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        // Each sorted one written out with a loop, which reads it as C# enumerates it.
        var names = "";
        foreach (var name in _names) names += "," + name;
        // As whole numbers, which print alike in every culture the server and the browser may run in.
        var prices = "";
        foreach (var price in _prices) prices += "," + (int)price;
        var index = "";
        foreach (var key in _index.Keys) index += "," + key;
        var ranks = "";
        foreach (var rank in _ranks) ranks += "," + rank;

        var page = new Column();
        page.Add(new Text($"roles {_roles.Contains("admin")} {_roles.Count}", TypeRole.BodyM));
        page.Add(new Text($"top {_stack.Peek()}", TypeRole.BodyM));
        page.Add(new Text($"next {_queue.Peek() + 1}", TypeRole.BodyM));
        page.Add(new Text($"list {_list.Count} {_list.Contains("b")}", TypeRole.BodyM));
        page.Add(new Text($"sorted {_sorted.Min} {_sorted.Count}", TypeRole.BodyM));
        page.Add(new Text($"names {names}", TypeRole.BodyM));
        page.Add(new Text($"prices {prices} {_prices.Count}", TypeRole.BodyM));
        page.Add(new Text($"index {index}", TypeRole.BodyM));
        page.Add(new Text($"ranks {ranks}", TypeRole.BodyM));
        page.Add(new Text($"amounts {_amounts.Contains(1.00m)}", TypeRole.BodyM));
        return page;
    }
}
