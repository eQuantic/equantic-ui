using System;
using System.Collections.Generic;

namespace eQuantic.UI.Web.Tests.Fixtures;

// A field and a local of each collection the runtime holds as a class of its own, and takes into
// typed variables: a field of one named a type its module never imported, and a local lost its
// element type, so tsc refused both (#1866).
public sealed class EmittedWaiting
{
    private readonly Queue<EmittedPointer> _queue = new();
    private readonly Stack<EmittedPointer> _stack = new();
    private readonly LinkedList<EmittedPointer> _list = new();
    private readonly SortedSet<int> _sorted = new();
    private readonly HashSet<EmittedPointer> _set = new();
    private readonly Queue<EmittedPointer> _pending = [];
    private readonly SortedSet<int> _ranks = [3, 1];
    private readonly HashSet<EmittedPointer> _seen = [];
    private EmittedPointer? _next;

    public int Count() => _queue.Count + _stack.Count + _list.Count + _sorted.Count + _set.Count;

    // Type arguments a module names with no import of its own (a date, a decimal, a span, a Guid), a
    // nullable reference, and a type parameter, which no module names.
    public int Spread<T>(T item)
    {
        var byDay = new Dictionary<DateTime, decimal>();
        var spans = new HashSet<TimeSpan>();
        var ids = new SortedSet<Guid>();
        var maybe = new Queue<EmittedPointer?>();
        var kept = new Stack<T>();
        var byName = new SortedDictionary<string, EmittedPointer>();
        var ranked = new SortedList<int, EmittedPointer>();
        kept.Push(item);
        byName["a"] = new EmittedPointer();
        ranked[1] = byName["a"];
        maybe.Enqueue(null);
        byDay[DateTime.UnixEpoch] = 1.5m;
        spans.Add(TimeSpan.Zero);
        ids.Add(Guid.Empty);
        return byDay.Count + spans.Count + ids.Count + maybe.Count + kept.Count + byName.Count + ranked.Count;
    }

    public bool Advance()
    {
        var queue = new Queue<EmittedPointer>();
        var stack = new Stack<EmittedPointer>();
        var list = new LinkedList<EmittedPointer>();
        var sorted = new SortedSet<int>();
        var set = new HashSet<EmittedPointer>();
        var map = new Dictionary<int, EmittedPointer>();
        var node = new LinkedListNode<EmittedPointer>(new EmittedPointer());
        Stack<EmittedPointer> undone = [];
        SortedSet<int> ranks = [2, 1];
        HashSet<EmittedPointer> seen = [node.Value];
        EmittedPointer held = node.Value;
        EmittedPointer? first = list.First?.Value;
        int least = sorted.Count > 0 ? sorted.Min : ranks.Min + _ranks.Max;
        set.Add(held);
        return queue.TryDequeue(out _next) || stack.TryPop(out _next) || map.TryGetValue(least, out _next)
            || first != null || _queue.TryPeek(out _next) || undone.TryPop(out _next)
            || _pending.TryDequeue(out _next) || seen.Contains(held) || _seen.Count > 0;
    }
}
