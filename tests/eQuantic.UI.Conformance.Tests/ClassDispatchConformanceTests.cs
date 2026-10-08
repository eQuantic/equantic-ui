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
/// (#612);</item>
/// <item>an exception class of the app's is a class, with its fields, its constructors, its base's
/// message, its inner exception and its methods, over the browser's <c>Error</c>, and a typed
/// <c>catch</c> takes it (#611).</item>
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
        public class Gen { public virtual string F<T>(List<T> x) => "base"; }
        public class GenHiding : Gen { public new string F<U>(List<U> x) => "hiding"; }
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
        ("a generic method that hides one over its own type parameter",
            "Gen g = new GenHiding(); return g.F(new List<int>()) + new GenHiding().F(new List<int>());"),
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
        public struct Walk : IEnumerator<int>
        {
            private int at;
            private readonly int to;
            public Walk(int to) { this.to = to; at = 0; }
            public int Current => at * 2;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++at <= to;
            public void Reset() { at = 0; }
            public void Dispose() { }
        }
        public class Fast : IEnumerable<int>
        {
            public Walk GetEnumerator() => new Walk(3);
            IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Ticker : IEnumerable<int>, IEnumerator<int>
        {
            private int at;
            public IEnumerator<int> GetEnumerator() { at = 0; return this; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public int Current => at;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++at <= 3;
            public void Reset() { at = 0; }
            public void Dispose() { }
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
        ("a public GetEnumerator of a struct beside the explicit ones",
            "var t = 0; foreach (var x in new Fast()) t += x; return t + \"|\" + string.Join(\",\", new Fast()) + \"|\" + new Fast().Sum();"),
        ("a class that is its own enumerator", "return string.Join(\",\", new Ticker()) + \"|\" + new Ticker().Count();"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClassThatImplementsIEnumerable_IsEnumeratedAsItsGetEnumeratorSays(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Sequences, typeAnnotations, SequenceCases);

    private const string Exceptions = """
        using System;
        using System.Collections.Generic;
        using App;
        namespace App
        {
            public class Failure : Exception
            {
                public int Code = 7;
                public List<int> Ids { get; } = new();
                public Failure() : base("failed") { Ids.Add(1); }
                public Failure(string message, Exception inner) : base(message, inner) { }
                public string Describe() => "f" + Code + ":" + Ids.Count;
            }
            public class Retry : Failure { public int Attempts { get; init; } = 3; public Retry() { Code = 9; } }
            public class Oops : Exception { }
            public class Custom : Exception
            {
                public Custom() : base("inner text") { }
                public override string Message => "custom:" + base.Message;
            }
            public class Gate : InvalidOperationException { public Gate(string m) : base(m) { } }
            public class Failed<T> : Exception
            {
                public T Value;
                public Failed(T value) : base("failed " + value) { Value = value; }
            }
            public class Coded(int number) : Exception("code " + number) { public int Code => number; }
            public class Closed : InvalidOperationException { }
            public class Shut : InvalidOperationException { public Shut() : base() { } }
            public class Nulled : InvalidOperationException { public Nulled() : base(null) { } }
            public class Maybe : InvalidOperationException { public Maybe(string? m) : base(m) { } }
            public class Bad : ArgumentException { public Bad(string name) : base("bad", name) { } }
            public class Missing : ArgumentNullException { public Missing(string name) : base(name) { } }
            public class Lookup : Exception
            {
                private readonly string key;
                public Lookup(string key) : base("missing " + key) { this.key = key; }
                public string Name => key;
            }
            public class Renamed : ArgumentException
            {
                public Renamed() : base("m", "p") { }
                public override string ParamName => "q";
            }
        }
        """;

    private static readonly (string Name, string Statements)[] ExceptionCases =
    [
        ("a field", "return new Failure().Code;"),
        ("a property with an initializer", "return new Failure(\"m\", null!).Ids.Count;"),
        ("a constructor's body", "return string.Join(\",\", new Failure().Ids);"),
        ("the base's message", "return new Failure().Message;"),
        ("an inner exception",
            "var f = new Failure(\"outer\", new InvalidOperationException(\"inner\")); return f.Message + \"|\" + f.InnerException!.Message + \"|\" + (f.InnerException is InvalidOperationException);"),
        ("no inner exception is null", "return new Failure().InnerException == null;"),
        ("a method", "return new Failure().Describe();"),
        ("a derived class's constructor over its base's", "var r = new Retry(); return r.Code + \"|\" + r.Attempts + \"|\" + r.Describe();"),
        ("an object initializer", "var r = new Retry { Attempts = 5 }; return r.Attempts + \"|\" + r.Code;"),
        ("a typed catch of the class",
            "try { throw new Retry(); } catch (ArgumentException) { return \"argument\"; } catch (Failure f) { return f.Describe() + \"|\" + (f is Retry) + \"|\" + f.Message; }"),
        ("a catch of the .NET type it derives from",
            "try { throw new Gate(\"closed\"); } catch (ArgumentException) { return \"argument\"; } catch (InvalidOperationException e) { return e.Message + \"|\" + (e is Gate); }"),
        ("a filter that reads a member",
            "try { throw new Coded(7); } catch (Coded c) when (c.Code == 8) { return \"eight\"; } catch (Coded c) when (c.Code == 7) { return \"seven\"; }"),
        ("the default message", "return new Oops().Message;"),
        ("an override of Message", "return new Custom().Message;"),
        ("a generic class, caught by its construction",
            "try { throw new Failed<int>(3); } catch (Failed<string>) { return \"string\"; } catch (Failed<int> f) { return f.Value + \"|\" + f.Message; }"),
        ("a primary constructor and its base clause", "var c = new Coded(4); return c.Code + \"|\" + c.Message;"),
        // What a .NET base's constructor writes and takes, as a `new` of that type hands it (#558).
        ("the text of the .NET base its constructor calls", "return new Closed().Message + \"|\" + new Shut().Message;"),
        ("a null message reads the text the .NET base writes for it",
            "return new Nulled().Message + \"|\" + new Maybe(null).Message + \"|\" + new Maybe(\"m\").Message;"),
        ("a parameter's name its .NET base takes", "var b = new Bad(\"x\"); return b.Message + \"|\" + b.ParamName;"),
        ("a .NET base that takes no message", "var m = new Missing(\"y\"); return m.Message + \"|\" + m.ParamName;"),
        ("a member named Name", "var l = new Lookup(\"k\"); return l.Name + \"|\" + l.Message;"),
        ("an override of ParamName", "var r = new Renamed(); return r.ParamName + \"|\" + r.Message;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExceptionOfTheAppsOwn_IsAClassOverTheBrowsersError(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Exceptions, typeAnnotations, ExceptionCases);

    /// <summary>
    /// Visual Studio's exception template, as the developer gets it: <c>()</c>, <c>(string)</c>,
    /// <c>(string, Exception)</c> and the protected <c>(SerializationInfo, StreamingContext)</c> only .NET's
    /// serialization calls, and a plain class with a constructor of that shape beside its own. The browser
    /// has no <c>SerializationInfo</c>, so that one is no branch of the twin: taken for one, it met the
    /// constructor of two arguments beside it and the build refused the class (EQ1009).
    /// </summary>
    private const string Template = """
        using System;
        using System.Runtime.Serialization;
        using App;
        namespace App
        {
            [Serializable]
            public class TemplatedException : Exception
            {
                public TemplatedException() { }
                public TemplatedException(string message) : base(message) { }
                public TemplatedException(string message, Exception inner) : base(message, inner) { }
        #pragma warning disable SYSLIB0051
                protected TemplatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        #pragma warning restore SYSLIB0051
            }
            public class Snapshot
            {
                public string First = "", Second = "";
                public Snapshot(string first, string second) { First = first; Second = second; }
                protected Snapshot(SerializationInfo info, StreamingContext context) : this(info.GetString("first")!, info.GetString("second")!) { }
            }
        }
        """;

    private static readonly (string Name, string Statements)[] TemplateCases =
    [
        ("each of the template's constructors",
            "var a = new TemplatedException(); var b = new TemplatedException(\"m\"); var c = new TemplatedException(\"outer\", new InvalidOperationException(\"inner\")); return a.Message + \"|\" + b.Message + \"|\" + c.Message + \"|\" + c.InnerException!.Message;"),
        ("a typed catch of the template's exception",
            "try { throw new TemplatedException(\"t\"); } catch (ArgumentException) { return \"argument\"; } catch (TemplatedException e) { return e.Message; }"),
        ("a plain class beside a constructor of the same shape", "var s = new Snapshot(\"a\", \"b\"); return s.First + s.Second;"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void VisualStudiosExceptionTemplate_BuildsAsItIsWritten(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Template, typeAnnotations, TemplateCases);
}
