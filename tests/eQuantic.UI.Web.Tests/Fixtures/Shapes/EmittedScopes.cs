using System;
using System.Collections.Generic;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

public sealed class EmittedScopes : EmittedScopedBase
{
    private static readonly Dictionary<int, EmittedPointer> Pointers = new();
    private readonly int _first = Find(0, out var first) ? first.Members.Count : 0;

    public EmittedScopes() : base(Find(1, out var held) ? held.Members.Count : 0) { }

    private static bool Find(int id, out EmittedPointer found)
    {
        found = Pointers.TryGetValue(id, out var pointer) ? pointer : new EmittedPointer();
        return Pointers.ContainsKey(id);
    }

    private static EmittedLease Lease(int id, out EmittedPointer held)
    {
        Find(id, out held);
        var total = 0;
        foreach (var member in held.Members) total += member.Length;
        return total > 0 ? new EmittedLease() : new EmittedLease();
    }

    public int While()
    {
        var total = 0;
        var id = 0;
        while (Pointers.TryGetValue(id++, out var next))
            foreach (var member in next.Members) total += member.Length;
        return total;
    }

    public int Do()
    {
        var id = 0;
        do { id++; } while (Pointers.TryGetValue(id, out var next) && next.Members.Count > 0);
        return id;
    }

    public int For()
    {
        var total = 0;
        for (var id = 0; Pointers.TryGetValue(id, out var next); id++)
            foreach (var member in next.Members) total += member.Length;
        return total;
    }

    public int Using(int id)
    {
        var total = 0;
        using (Lease(id, out var held))
            foreach (var member in held.Members) total += member.Length;
        return total;
    }

    public int Filter(Exception thrown)
    {
        var total = 0;
        try { throw thrown; }
        catch (Exception e) when (Pointers.TryGetValue(e.Message.Length, out var held))
        {
            foreach (var member in held.Members) total += member.Length;
        }
        return total;
    }

    public int Section(int kind)
    {
        var total = 0;
        switch (kind)
        {
            case 1:
                Pointers.TryGetValue(kind, out var held);
                foreach (var member in held!.Members) total += member.Length;
                break;
        }
        return total;
    }

    public int Guarded(int kind)
    {
        var total = 0;
        switch (kind)
        {
            case > 1 when Pointers.TryGetValue(kind, out var held):
                foreach (var member in held.Members) total += member.Length;
                break;
        }
        return total;
    }

    public int Arm(int kind) => kind switch
    {
        > 0 when Pointers.TryGetValue(kind, out var held) => held.Members.Count + _first,
        _ => 0,
    };

    private static readonly Dictionary<int, List<string>> Lists = new();

    public int Arms(int kind) => kind switch
    {
        > 5 when Pointers.TryGetValue(kind, out var found) => found.Members.Count,
        > 0 when Lists.TryGetValue(kind, out var found) => found.Count,
        _ => 0,
    };

    public int Filters(Exception thrown)
    {
        try { throw thrown; }
        catch (ArgumentException e) when (Pointers.TryGetValue(e.Message.Length, out var found)) { return found.Members.Count; }
        catch (Exception e) when (Lists.TryGetValue(e.Message.Length, out var found)) { return found.Count; }
    }

    public int Guards(object value)
    {
        switch (value)
        {
            case int id when Pointers.TryGetValue(id, out var found):
                return found.Members.Count;
            case string found:
                return found.Length;
        }
        return 0;
    }
}
