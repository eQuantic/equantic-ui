using System;
using System.Collections.Generic;

namespace eQuantic.UI.Web.Tests.Fixtures;

// A static generic class, whose statics are each closed type's (#1855): every member is static, and a
// static member that names its class's type parameter was refused (TS2302), so the twin declares none.
public static class EmittedCache<T>
{
    public const int Size = 4;
    public static T? Last;
    public static int Hits { get; private set; }
    private static readonly List<T> _all = new();
    static EmittedCache() { Hits = 0; }
    public static int Count => _all.Count;
    public static T Keep(T value) { Last = value; _all.Add(value); Hits++; Func<T, T> echo = Echo; return echo(value); }
    private static T Echo(T value) { T Local(T v) => v; return Local(value); }
}
