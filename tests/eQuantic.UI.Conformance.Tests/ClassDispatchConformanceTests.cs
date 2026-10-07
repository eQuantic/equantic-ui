using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A class of the app's dispatches and enumerates in the browser as it does in .NET, run through the
/// module graph an app's build writes, both sides executed:
/// <list type="bullet">
/// <item>a method that hides an inherited member is reached as the hiding one only through a type that
/// declares it, and a call bound to the hidden member reaches the hidden one, the runtime's own included
/// (#563);</item>
/// <item>a class that implements <c>IEnumerable&lt;T&gt;</c> is enumerated as its own
/// <c>GetEnumerator()</c> says, by a <c>foreach</c>, a spread, <c>string.Join</c> and LINQ alike
/// (#612).</item>
/// </list>
/// </summary>
public class ClassDispatchConformanceTests
{
    private const string Hiding = """
        using System;
        using System.Collections.Generic;
        public class A { public virtual string Name() => "A"; public string Call() => Name(); }
        public class B : A { public new string Name() => "B"; public string Both() => Name() + base.Name(); }
        public class D : B { public new virtual string Name() => "D"; }
        public class E : D { public override string Name() => "E" + base.Name(); }
        public class Up : B { public string Base() => base.Name(); }
        #pragma warning disable CS0108
        public class Quiet : A { public string Name() => "Q"; }
        #pragma warning restore CS0108
        public class Maker { public static string Make() => "S"; }
        public class Remaker : Maker { public new static string Make() => "M"; public static string Both() => Make() + Maker.Make(); }
        public class Sized { public int Size = 1; }
        public class Measured : Sized { public new int Size() => 2; }
        public class Box<T> { public virtual string Get(T x) => "box"; }
        public class IntBox : Box<int> { public new string Get(int x) => "int"; }
        public class Counter { public int Calls; public new int GetHashCode() => ++Calls; }
        public class Labelled { public new string ToString() => "hidden"; }
        """;

    private static readonly (string Name, string Statements)[] HidingCases =
    [
        ("a call through the base reaches the base's", "A a = new B(); return a.Name();"),
        ("a call through the hiding type reaches the hiding one", "B b = new B(); return b.Name();"),
        ("the base's own call reaches the base's", "return new B().Call();"),
        ("the hiding class reaches its own and its base's", "return new B().Both();"),
        ("a derived class's base call reaches the hiding one", "var u = new Up(); return u.Base() + u.Name() + ((A)u).Name();"),
        ("an override of a virtual that hides one fills its slot",
            "var e = new E(); A a = e; B b = e; D d = e; return a.Name() + \"|\" + b.Name() + \"|\" + d.Name();"),
        ("a method group of each", "A a = new B(); Func<string> f = a.Name; Func<string> g = new B().Name; return f() + g();"),
        ("a null-conditional call of each", "B? b = new B(); A? a = b; return a?.Name() + b?.Name();"),
        ("a method that hides one without saying new", "A a = new Quiet(); return a.Name() + new Quiet().Name();"),
        ("a static that hides one", "return Maker.Make() + Remaker.Make() + Remaker.Both();"),
        ("a method that hides a field", "var m = new Measured(); return m.Size() + \"|\" + ((Sized)m).Size;"),
        ("a method that hides one of a generic base", "Box<int> b = new IntBox(); return b.Get(1) + new IntBox().Get(1);"),
        ("an object's hash is object's, not the method that hides it",
            "var c = new Counter(); object o = c; var h1 = o.GetHashCode(); var h2 = o.GetHashCode(); return (h1 == h2) + \"|\" + c.Calls;"),
        ("a combined hash asks object's, not the method that hides it", "var c = new Counter(); HashCode.Combine(c, 1); return c.Calls;"),
        ("the method that hides GetHashCode, called through its type", "var c = new Counter(); c.GetHashCode(); return c.GetHashCode();"),
        ("the method that hides ToString, called through its type", "return new Labelled().ToString();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AMethodThatHidesOne_IsReachedThroughItsBase_AsDotNetReachesIt(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Hiding, typeAnnotations, HidingCases);

    private const string Sequences = """
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq;
        public class Bag : IEnumerable<int>
        {
            private readonly List<int> items = new();
            public void Add(int x) => items.Add(x * 10);
            public IEnumerator<int> GetEnumerator() { foreach (var item in items) yield return item; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Countdown : IEnumerable<int>
        {
            private readonly int from;
            public Countdown(int from) { this.from = from; }
            public IEnumerator<int> GetEnumerator() => new CountdownEnumerator(from);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class CountdownEnumerator : IEnumerator<int>
        {
            public static int Disposed;
            private int at;
            public CountdownEnumerator(int from) { at = from + 1; }
            public int Current => at;
            object IEnumerator.Current => Current;
            public bool MoveNext() => --at > 0;
            public void Reset() { }
            public void Dispose() => Disposed++;
        }
        public struct Steps : IEnumerator<int>
        {
            private int at;
            private readonly int to;
            public Steps(int to) { this.to = to; at = 0; }
            public int Current => at;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++at <= to;
            public void Reset() { at = 0; }
            public void Dispose() { }
        }
        public class Stair : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() => new Steps(3);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Letters : IEnumerable<string>
        {
            IEnumerator<string> IEnumerable<string>.GetEnumerator() { yield return "a"; yield return "b"; }
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<string>)this).GetEnumerator();
        }
        public abstract class Shelf : IEnumerable<string>
        {
            public abstract IEnumerator<string> GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Books : Shelf { public override IEnumerator<string> GetEnumerator() { yield return "x"; yield return "y"; } }
        public class Loose : IEnumerable
        {
            public IEnumerator GetEnumerator() { yield return 1; yield return "two"; }
        }
        public record Pair(int First, int Second) : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() { yield return First; yield return Second; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        """;

    private static readonly (string Name, string Statements)[] SequenceCases =
    [
        ("foreach", "var bag = new Bag(); bag.Add(3); var t = 0; foreach (var x in bag) t += x; return t;"),
        ("string.Join", "var bag = new Bag(); bag.Add(1); bag.Add(2); return string.Join(\",\", bag);"),
        ("a LINQ call", "var bag = new Bag { 1, 2, 3 }; return bag.Where(x => x > 10).Sum() + \"|\" + bag.Count();"),
        ("ToList", "var bag = new Bag { 1, 2 }; var list = bag.ToList(); list.Add(0); return string.Join(\"|\", list) + \"|\" + bag.Count();"),
        ("a spread", "var bag = new Bag { 4, 5 }; int[] all = [0, .. bag]; return string.Join(\"|\", all);"),
        ("as the non-generic IEnumerable", "IEnumerable e = new Bag { 7 }; var r = \"\"; foreach (var o in e) r += o; return r;"),
        ("an enumerator the app wrote, walked and disposed",
            "CountdownEnumerator.Disposed = 0; var s = \"\"; foreach (var n in new Countdown(3)) s += n; return s + \"|\" + CountdownEnumerator.Disposed;"),
        ("an enumerator the app wrote, under LINQ",
            "CountdownEnumerator.Disposed = 0; return string.Join(\",\", new Countdown(4).Select(n => n * 2)) + \"|\" + CountdownEnumerator.Disposed;"),
        ("a loop that breaks disposes its enumerator",
            "CountdownEnumerator.Disposed = 0; foreach (var n in new Countdown(5)) { if (n == 4) break; } return CountdownEnumerator.Disposed;"),
        ("a struct's enumerator", "return string.Join(\",\", new Stair()) + \"|\" + new Stair().Count();"),
        ("an explicit implementation of IEnumerable<T>", "var l = new Letters(); return string.Join(\"\", l) + l.Count();"),
        ("an abstract GetEnumerator its derived class writes",
            "Shelf s = new Books(); var r = \"\"; foreach (var b in s) r += b; return r + string.Join(\"\", new Books().Reverse());"),
        ("a class that implements only IEnumerable", "var r = \"\"; foreach (var o in new Loose()) r += o; return r;"),
        ("a record's sequence", "var p = new Pair(3, 4); return string.Join(\",\", p) + \"|\" + p.Sum();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassThatImplementsIEnumerable_IsEnumeratedAsItsGetEnumeratorSays(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Sequences, typeAnnotations, SequenceCases);
}
