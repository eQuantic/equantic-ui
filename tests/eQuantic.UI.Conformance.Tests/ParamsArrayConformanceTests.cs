using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A <c>params</c> parameter of a method is the array it is in C#: a call that lists its elements packs them, and an
/// array handed whole is passed as it is. It was a rest parameter, so a whole array was spread past an engine's
/// argument cap and the callee got a copy (#1159). A constructor's stays a rest parameter, which its overloads
/// dispatch on (#1192).
/// </summary>
public class ParamsArrayConformanceTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        public class Bag
        {
            public int Count(params int[] xs) => xs.Length;
            public static int Sum(int seed, params int[] xs) { foreach (var x in xs) seed += x; return seed; }
            public int[] Zero(params int[] xs) { xs[0] = 0; return xs; }
            public string Names(params string[] names) => string.Join(",", names);
            public int Objects(params object[] items) => items.Length;
            public virtual int Virtual(params int[] xs) => xs.Length;
            public bool IsNull(params int[] xs) => xs == null;
            public int Default(int a = 1, params int[] xs) => a + xs.Length;
        }
        public class Derived : Bag { public override int Virtual(params int[] xs) => xs.Length * 10; public int Base(params int[] xs) => base.Virtual(1, 2, 3) + xs.Length; }
        public interface ICounts { int Many(params int[] xs); }
        public class Counts : ICounts { public int Many(params int[] xs) => xs.Length; }
        public static class Ext { public static int Tail(this string head, params int[] xs) => head.Length + xs.Length; }
        public delegate int Counter(params int[] xs);
        public static class Calls { public static int Twice(params int[] xs) => xs.Length * 2; public static int Generic<T>(params T[] xs) => xs.Length; }
        """;

    [SkippableTheory]
    // The rows of #1159.
    [InlineData("var big = new int[1000000]; return new Bag().Count(big);")]
    [InlineData("var a = new[] { 1 }; var r = new Bag().Zero(a); return a[0] + \"|\" + object.ReferenceEquals(a, r);")]
    // Each form of a call: nothing, elements, a whole array, a named argument.
    [InlineData("var b = new Bag(); return b.Count() + \"|\" + b.Count(1) + \"|\" + b.Count(1, 2) + \"|\" + b.Count(new[] { 1, 2, 3 });")]
    [InlineData("return Bag.Sum(1) + \"|\" + Bag.Sum(1, 2) + \"|\" + Bag.Sum(1, 2, 3) + \"|\" + Bag.Sum(1, new[] { 2, 3, 4 }) + \"|\" + Bag.Sum(xs: new[] { 2 }, seed: 1);")]
    [InlineData("var b = new Bag(); return b.Names() + \"|\" + b.Names(\"a\", \"b\") + \"|\" + b.Names(new[] { \"c\" });")]
    [InlineData("var b = new Bag(); return b.IsNull(null).ToString();")]
    // An array of ints is ONE element of a params object[].
    [InlineData("var b = new Bag(); int[] one = { 1, 2 }; object[] two = { 1, 2 }; return b.Objects(one) + \"|\" + b.Objects(two) + \"|\" + b.Objects(1, \"a\");")]
    // Through a base, an interface, an extension, a delegate and a method group.
    [InlineData("Bag b = new Derived(); return b.Virtual(1, 2) + \"|\" + b.Virtual(new[] { 1 });")]
    [InlineData("ICounts c = new Counts(); return c.Many() + \"|\" + c.Many(1, 2) + \"|\" + c.Many(new[] { 1, 2, 3 });")]
    [InlineData("return \"ab\".Tail() + \"|\" + \"ab\".Tail(1, 2) + \"|\" + Ext.Tail(\"ab\", new[] { 1 });")]
    [InlineData("Counter c = Calls.Twice; return c() + \"|\" + c(1, 2) + \"|\" + c(new[] { 1 });")]
    [InlineData("Counter c = xs => xs.Length; return c() + \"|\" + c(1, 2);")]
    [InlineData("Func<int[], int> f = Calls.Twice; return f(new[] { 1, 2 }).ToString();")]
    // A local function.
    [InlineData("int Local(params int[] xs) => xs.Length; return Local() + \"|\" + Local(1, 2) + \"|\" + Local(new[] { 1 });")]
    // An element that awaits, and the order of evaluation.
    [InlineData("var log = \"\"; int E(int n) { log += n; return n; } var b = new Bag(); var c = b.Count(E(1), E(2)); return log + \"|\" + c;")]
    // An optional parameter left out before it, a guarded receiver, a base call, a generic method and a lambda.
    [InlineData("var b = new Bag(); return b.Default() + \"|\" + b.Default(2) + \"|\" + b.Default(2, 3, 4) + \"|\" + b.Default(xs: new[] { 1 });")]
    [InlineData("Bag b = new Bag(); return (b?.Count(1, 2)).ToString() + \"|\" + (b?.Count(new[] { 1 })) + \"|\" + (b?.Count());")]
    [InlineData("return new Derived().Base(1, 2).ToString();")]
    [InlineData("return Calls.Generic(\"a\", \"b\") + \"|\" + Calls.Generic<int>() + \"|\" + Calls.Generic(new[] { 1, 2, 3 });")]
    [InlineData("var f = (params int[] xs) => xs.Length; return f() + \"|\" + f(1, 2) + \"|\" + f(new[] { 1 });")]
    public void AParamsParameter_IsTheArrayItIs(string statements) =>
        ModuleGraph.AssertSameAsDotNet(Source, false, ("case", statements));

    /// <summary>A constructor's <c>params</c> parameter, which stayed a rest parameter: an array handed whole was
    /// spread into the call, past an engine's argument cap, and the constructor got a copy where .NET hands it the
    /// caller's array (#1192). Its overloads, a chain through <c>: this</c>, a base's through <c>: base</c>, a
    /// record's primary constructor and a named argument, each executed on both sides.</summary>
    private const string Constructors = """
        public class Pack { public int[] Items; public Pack(params int[] items) { Items = items; } public int Count => Items.Length; }
        public class Named { public string First; public string[] Rest; public Named(string first, params string[] rest) { First = first; Rest = rest; } }
        public class Many { public int Kind = -1; public int[] Xs = new int[0]; public Many() { Kind = 0; } public Many(int kind, params int[] xs) { Kind = kind; Xs = xs; } }
        public class Chained { public int[] Xs; public Chained(params int[] xs) { Xs = xs; } public Chained(string s, int n) : this(1, 2, n) { } }
        public class BaseP { public int[] Xs; public BaseP(params int[] xs) { Xs = xs; } }
        public class DerivedP : BaseP { public DerivedP() : base(4, 5) { } public DerivedP(int[] all) : base(all) { } }
        public record RecP(params int[] Xs);
        """;

    [SkippableTheory]
    [InlineData("var big = new int[1000000]; return new Pack(big).Count.ToString();")]                                            // 1000000
    [InlineData("var a = new[] { 1, 2 }; var p = new Pack(a); a[0] = 9; return p.Items[0] + \"|\" + object.ReferenceEquals(a, p.Items);")] // 9|True
    [InlineData("return new Pack().Count + \"|\" + new Pack(1).Count + \"|\" + new Pack(1, 2, 3).Count;")]                        // 0|1|3
    [InlineData("var n = new Named(\"a\", \"b\", \"c\"); var e = new Named(\"z\"); return n.First + \"|\" + string.Join(\",\", n.Rest) + \"|\" + e.Rest.Length;")] // a|b,c|0
    [InlineData("var n = new Named(first: \"x\", rest: new[] { \"y\" }); return n.First + \"|\" + string.Join(\",\", n.Rest);")] // x|y
    [InlineData("var m = new Many(); var m2 = new Many(7); var m3 = new Many(7, 1, 2); return m.Kind + \"|\" + m2.Kind + \"|\" + m2.Xs.Length + \"|\" + m3.Xs.Length;")] // 0|7|0|2
    [InlineData("var c = new Chained(\"s\", 7); return c.Xs.Length + \"|\" + c.Xs[2] + \"|\" + new Chained(5, 6).Xs.Length;")]                // 3|7|2
    [InlineData("var all = new[] { 1, 2, 3 }; var d = new DerivedP(all); return new DerivedP().Xs.Length + \"|\" + d.Xs.Length + \"|\" + object.ReferenceEquals(all, d.Xs);")] // 2|3|True
    [InlineData("var a = new[] { 5 }; var r = new RecP(1, 2); var s = new RecP(a); return r.Xs.Length + \"|\" + object.ReferenceEquals(a, s.Xs);")] // 2|True
    public void AConstructorsParamsParameter_IsTheArrayItIs(string statements) =>
        ModuleGraph.AssertSameAsDotNet(Constructors, false, ("case", statements));
}
