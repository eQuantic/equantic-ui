using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An <c>Equals</c> the app wrote is asked every time .NET asks it, a value compared with itself included: only a
/// record's synthesized <c>Equals</c> checks the reference first, so an identity check anywhere else answers for
/// code that never ran. The counters show the calls, and <c>Refuses</c>, which is unequal to itself, the answer (#979).
/// </summary>
public class AnAppsEqualityConformanceTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        public class Counted { public static int Calls; public override bool Equals(object o) { Calls++; return ReferenceEquals(this, o); } public override int GetHashCode() => 1; }
        public class Refuses { public override bool Equals(object o) => false; public override int GetHashCode() => 1; }
        public class Plain { }
        public class Derived : Plain { public override bool Equals(object o) { Counted.Calls++; return ReferenceEquals(this, o); } public override int GetHashCode() => 1; }
        public class Typed : IEquatable<Typed> { public static int Calls; public bool Equals(Typed o) { Calls++; return ReferenceEquals(this, o); } public override bool Equals(object o) => false; public override int GetHashCode() => 1; }
        public record Holder(Counted C);
        public struct Box { public Counted C; public Box(Counted c) { C = c; } }
        public record struct Wrap(Typed T);
        public record struct Own(int V) { public static int Calls; public bool Equals(Own o) { Calls++; return V == o.V; } public override int GetHashCode() => V; }
        """;

    [SkippableTheory]
    // An instance call through a base that overrides nothing, and through object.
    [InlineData("var c = new Counted(); object o = c; return o.Equals(o) + \"|\" + Counted.Calls;")]
    [InlineData("Plain d = new Derived(); return d.Equals(d) + \"|\" + Counted.Calls;")]
    [InlineData("object r = new Refuses(); return r.Equals(r).ToString();")]
    // A member compared inside a twin.
    [InlineData("var c = new Counted(); var a = new Holder(c); var b = new Holder(c); return a.Equals(b) + \"|\" + Counted.Calls + \"|\" + (a == b) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); var a = new Box(c); return a.Equals(a) + \"|\" + Counted.Calls;")]
    // A record's synthesized Equals checks the reference first.
    [InlineData("var a = new Holder(new Counted()); return a.Equals(a) + \"|\" + Counted.Calls + \"|\" + (a == a) + \"|\" + Counted.Calls;")]
    // The default comparer over a record struct whose member compares by a typed Equals of the app's.
    [InlineData("var w = new Wrap(new Typed()); return EqualityComparer<Wrap>.Default.Equals(w, w) + \"|\" + Typed.Calls;")]
    [InlineData("var w = new Wrap(new Typed()); return w.Equals(w) + \"|\" + Typed.Calls;")]
    [InlineData("var w = new Own(1); return w.Equals(w) + \"|\" + Own.Calls + \"|\" + EqualityComparer<Own>.Default.Equals(w, w) + \"|\" + Own.Calls;")]
    // A search over a collection.
    [InlineData("var c = new Counted(); return new List<Counted> { c }.Contains(c) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); var l = new List<Counted> { c }; return l.IndexOf(c) + \"|\" + l.LastIndexOf(c) + \"|\" + Counted.Calls + \"|\" + l.Remove(c) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); return new[] { c }.Contains(c) + \"|\" + Array.IndexOf(new[] { c }, c) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); var s = new HashSet<Counted> { c }; var r = s.Contains(c); return r + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); var d = new Dictionary<Counted, int> { [c] = 1 }; var r = d.ContainsKey(c); return r + \"|\" + Counted.Calls;")]
    [InlineData("var r = new Refuses(); return new List<Refuses> { r }.Contains(r) + \"|\" + new HashSet<Refuses> { r }.Contains(r);")]
    [InlineData("var r = new Refuses(); var d = new Dictionary<Refuses, int> { [r] = 1 }; return d.ContainsKey(r) + \"|\" + d.Count;")]
    [InlineData("var c = new Typed(); return new List<Typed> { c }.Contains(c) + \"|\" + new HashSet<Typed> { c }.Contains(c) + \"|\" + Typed.Calls;")]
    [InlineData("var c = new Counted(); var l = new[] { c, c }; return l.Distinct().Count() + \"|\" + Counted.Calls;")]
    // Composite values compare each member with its type's comparer.
    [InlineData("var c = new Counted(); return (c, 1).Equals((c, 1)) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); return new { C = c }.Equals(new { C = c }) + \"|\" + Counted.Calls;")]
    [InlineData("var c = new Counted(); return new KeyValuePair<Counted, int>(c, 1).Equals(new KeyValuePair<Counted, int>(c, 1)).ToString();")]
    // Generic code, which settles it at run time.
    [InlineData("static bool Same<T>(T a, T b) => EqualityComparer<T>.Default.Equals(a, b); var c = new Counted(); return Same(c, c) + \"|\" + Counted.Calls;")]
    [InlineData("static bool Has<T>(List<T> l, T v) => l.Contains(v); var c = new Counted(); return Has(new List<Counted> { c }, c) + \"|\" + Counted.Calls;")]
    public void AnEqualsOfTheApps_IsAskedAsDotNetAsksIt(string statements) =>
        ModuleGraph.AssertSameAsDotNet(Source, false, ("case", "Counted.Calls = 0; Typed.Calls = 0; Own.Calls = 0; " + statements));
}
