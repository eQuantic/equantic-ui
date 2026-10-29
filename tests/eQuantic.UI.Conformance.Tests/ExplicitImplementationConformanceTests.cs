using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A type's explicit interface implementations answer every call through each interface as .NET answers
/// it, run through the module graph an app's build writes, both sides executed (#1139). Each is written
/// under the name of the member it implements, which a call through the interface reaches, and the twin
/// holds a name once: one that hands back what the member holding its name answers is not written, so
/// that member answers for both interfaces, where the later of two was written over the other and,
/// handing off to it, called itself until the stack overflowed. A method of the type's own that answers
/// another interface beside one that hands off to it was refused, and is the one that answers.
/// </summary>
public class ExplicitImplementationConformanceTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Linq;
        public interface IFoo { string Run(); }
        public interface IBar { string Run(); }
        public interface IVal { int Value { get; } }
        public interface IOther { int Value { get; } }
        public interface ITake { string Take(int x, string y); }
        public interface IGive { string Take(int x, string y); }
        public class Forwarded : IFoo, IBar
        {
            string IFoo.Run() => "foo";
            string IBar.Run() => ((IFoo)this).Run();
        }
        public class Mixed : IFoo, IBar
        {
            public string Run() => "own";
            string IFoo.Run() => Run();
        }
        public class Labeled : IFoo
        {
            public string Run() => "own";
            string IFoo.Run() => "interface";
        }
        public class Relayed : IFoo, IBar
        {
            public string Run() => ((IFoo)this).Run() + "!";
            string IFoo.Run() => "foo";
            string IBar.Run() => ((IFoo)this).Run();
        }
        public class Held : IVal, IOther
        {
            public int Value => 5;
            int IVal.Value => Value;
        }
        public class Valued : IVal, IOther
        {
            int IVal.Value => 1;
            int IOther.Value => ((IVal)this).Value;
        }
        public class Passing : ITake, IGive
        {
            string ITake.Take(int x, string y) => y + x;
            string IGive.Take(int x, string y) => ((ITake)this).Take(x, y);
        }
        public class Base { public string Run() => "base"; }
        public class Derived : Base, IFoo { string IFoo.Run() => Run(); }
        public class Via : IFoo
        {
            public string Run() => "own";
            public virtual string Other() => Run();
            string IFoo.Run() => Other();
        }
        public class ViaDerived : Via { public override string Other() => "derived"; }
        public interface ILong { long Wide(); }
        public class Widened : ILong
        {
            public int Wide() => 1;
            long ILong.Wide() => Wide();
        }
        public struct Spot { public int X; }
        public interface IBoxed { object Get(); }
        public class Boxer : IBoxed
        {
            private Spot saved;
            public Spot Get() => saved;
            object IBoxed.Get() => Get();
        }
        public class Slot : IFoo, IBar
        {
            string IFoo.Run() => Other();
            string IBar.Run() => ((IFoo)this).Run();
            private string Other() => "slot";
        }
        public interface IOut { void Run(out int result); }
        public class Outs : IOut
        {
            public void Run(out int value) { value = 42; }
            void IOut.Run(out int result) => Run(out result);
        }
        public interface IMaybe { string? Maybe(); }
        public class Caster : IMaybe
        {
            public object Maybe() => 5;
            string? IMaybe.Maybe() => Maybe() as string;
        }
        public interface ICount { int Count(int[] xs); }
        public class Counted : ICount
        {
            public int Count(params int[] xs) => xs.Length;
            int ICount.Count(int[] xs) => Count(xs);
        }
        public static class Through
        {
            public static string Foo<T>(T value) where T : IFoo => value.Run();
            public static string Bar<T>(T value) where T : IBar => value.Run();
        }
        """;

    private static readonly (string Name, string Statements)[] Cases =
    [
        ("two interfaces of one name, one handing off to the other's, called through each",
            "var f = new Forwarded(); IFoo foo = f; IBar bar = f; return foo.Run() + \"|\" + bar.Run();"),
        ("an implicit method that answers one interface beside an explicit one that hands off to it",
            "var m = new Mixed(); IFoo foo = m; IBar bar = m; return m.Run() + \"|\" + foo.Run() + \"|\" + bar.Run();"),
        ("an implicit method beside an explicit one with an answer of its own",
            "var l = new Labeled(); IFoo foo = l; return l.Run() + \"|\" + foo.Run();"),
        ("an implicit method that hands off to an explicit one, which another one hands off to",
            "var r = new Relayed(); IFoo foo = r; IBar bar = r; return r.Run() + \"|\" + foo.Run() + \"|\" + bar.Run();"),
        ("an explicit property beside the type's own, which it reads",
            "var h = new Held(); IVal val = h; IOther other = h; return h.Value + \"|\" + val.Value + \"|\" + other.Value;"),
        ("two explicit properties, one reading the other",
            "var v = new Valued(); IVal val = v; IOther other = v; return val.Value + \"|\" + other.Value;"),
        ("two explicit methods, one passing its parameters to the other",
            "var p = new Passing(); ITake take = p; IGive give = p; return take.Take(1, \"a\") + \"|\" + give.Take(2, \"b\");"),
        ("an explicit one that hands off to a base's method",
            "var d = new Derived(); IFoo foo = d; Base b = d; return foo.Run() + \"|\" + d.Run() + \"|\" + b.Run();"),
        ("a call through a constrained type parameter of each interface",
            "return Through.Foo(new Forwarded()) + \"|\" + Through.Bar(new Forwarded()) + \"|\" + Through.Bar(new Mixed());"),
        ("an explicit one that hands off through an overridable method, over a type that overrides it",
            "IFoo a = new Via(); IFoo b = new ViaDerived(); return a.Run() + \"|\" + b.Run() + \"|\" + new ViaDerived().Run();"),
        ("an explicit one that widens what it hands back to a long",
            "ILong w = new Widened(); object o = w.Wide(); return (o is long) + \"|\" + (w.Wide() + 1L) + \"|\" + new Widened().Wide();"),
        ("an explicit one that boxes a struct, each call a copy of its own",
            "IBoxed b = new Boxer(); return ReferenceEquals(b.Get(), b.Get());"),
        ("an explicit one handing off to another that hands off under another name",
            "var s = new Slot(); IFoo foo = s; IBar bar = s; return foo.Run() + \"|\" + bar.Run();"),
        ("an explicit one passing an out through", "IOut o = new Outs(); o.Run(out var r); var v = 0; new Outs().Run(out v); return r + \"|\" + v;"),
        ("an explicit one handing back an as", "IMaybe m = new Caster(); return (m.Maybe() is null) + \"|\" + new Caster().Maybe();"),
        ("an explicit one handing an array to a params parameter",
            "ICount c = new Counted(); return c.Count(new[] { 1, 2 }) + \"|\" + new Counted().Count(1, 2, 3);"),
        ("each interface over a list of them",
            "var all = new List<IBar> { new Forwarded(), new Mixed(), new Relayed() }; return string.Join(\",\", all.Select(x => x.Run()));"),
    ];

    [SkippableTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnExplicitImplementation_AnswersThroughEachInterface_AsDotNetDoes(bool typeAnnotations) =>
        ModuleGraph.AssertSameAsDotNet(Source, typeAnnotations, Cases);
}
