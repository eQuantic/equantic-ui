using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Conformance.Tests.Hydration;

/// <summary>
/// A page whose prefetch fills the collections the browser holds as its own classes: a set, a stack,
/// a queue of longs, a linked list and a sorted set. Each crossed as the array the server writes, into
/// code that reads a Set or the runtime's class (#516).
/// </summary>
[Page("/crossing-collections")]
public sealed class CrossingCollectionsPage : StatefulComponent, IServerPrefetch
{
    private readonly HashSet<string> _roles = new();
    private readonly Stack<int> _stack = new();
    private readonly Queue<long> _queue = new();
    private readonly LinkedList<string> _list = new();
    private readonly SortedSet<int> _sorted = new();

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
        return Task.CompletedTask;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var page = new Column();
        page.Add(new Text($"roles {_roles.Contains("admin")} {_roles.Count}", TypeRole.BodyM));
        page.Add(new Text($"top {_stack.Peek()}", TypeRole.BodyM));
        page.Add(new Text($"next {_queue.Peek() + 1}", TypeRole.BodyM));
        page.Add(new Text($"list {_list.Count} {_list.Contains("b")}", TypeRole.BodyM));
        page.Add(new Text($"sorted {_sorted.Min} {_sorted.Count}", TypeRole.BodyM));
        return page;
    }
}
