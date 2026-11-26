using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedArena
{
    private readonly Dictionary<int, EmittedPointer> _arenas = new();
    public int Count(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var arena)) return 0;
        var total = 0;
        foreach (var member in arena.Members) total += member.Length;
        return total;
    }

    public bool Find(int pointer, out EmittedPointer? found)
    {
        found = _arenas.TryGetValue(pointer, out var arena) ? arena : null;
        return found != null;
    }

    public int Forget(int pointer)
    {
        if (Find(pointer, out var found)) found = null;
        return found == null ? 0 : 1;
    }

    private readonly Dictionary<int, List<EmittedPointer>> _routes = new();
    public int Routed(int pointer)
    {
        var total = 0;
        if (_routes.TryGetValue(pointer, out var routed))
            foreach (var route in routed) total += route.Members.Count;
        return total;
    }

    public int Drop(int pointer)
    {
        if (_arenas.TryGetValue(pointer, out EmittedPointer? seen)) seen = null;
        return seen == null ? 0 : 1;
    }

    private EmittedPointer? _last;
    public bool Remember(int pointer) => _arenas.TryGetValue(pointer, out _last);
}
