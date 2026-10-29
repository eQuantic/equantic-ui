using System.Linq;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An explicit interface implementation holds the name of the member it implements on its type's twin,
/// once (#1139). A call through an interface reaches its member by that name, whatever type implements
/// it, and a JavaScript class holds one member per name: two explicit implementations of one name were
/// both written and the browser kept the last, and one that handed off to the other, written last, called
/// itself. One that hands back what the member holding its name answers is not written; what the twin
/// would write twice on one name along its chain is refused (EQ1007), naming the interfaces.
/// </summary>
public class ExplicitImplementationTests
{
    private const string Faces = """
        public interface IFoo { string Run(); }
        public interface IBar { string Run(); }
        public interface IVal { int Value { get; } }
        public interface IOther { int Value { get; } }
        public interface ITake { string Take(int x, string y); }
        public interface IGive { string Take(int x, string y); }
        public interface IMake { static abstract string Make(); }
        public interface IGet<T> { string Get(); }
        public interface ILong { long Run(); }
        public interface IObj { object Run(); }
        public interface IOut { void Run(out int result); }
        public interface IMaybe { string? Run(); }
        public interface IProbe { bool Equals(object other); }
        public interface ICount { int Run(int[] xs); }
        public interface ITask { System.Threading.Tasks.Task Run(); }
        public interface IText { string ToString { get; } }
        """;

    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(Faces + "\n" + source, "Probe.cs").Single(result => result.ComponentName == name);

    /// <summary>How many members of <paramref name="name"/> a twin declares: a method, an async one, a getter, a
    /// static.</summary>
    private static int Declared(string typeScript, string name) =>
        Regex.Matches(typeScript, $@"^\s*(static\s+)?(async\s+)?(get\s+)?{Regex.Escape(name)}\(", RegexOptions.Multiline).Count;

    public static TheoryData<string, string, string, int, bool> HandOffs => new()
    {
        {
            "an explicit one through another interface's", """
            public class Fwd : IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => ((IFoo)this).Run();
            }
            """, "run", 1, false
        },
        {
            "an explicit one to the type's own method, which answers another interface", """
            public class Mixed : IFoo, IBar
            {
                public string Run() => "own";
                string IFoo.Run() { return Run(); }
            }
            """, "run", 1, false
        },
        {
            "an explicit property to the type's own", """
            public class Held : IVal, IOther
            {
                public int Value => 5;
                int IVal.Value => Value;
            }
            """, "value", 1, false
        },
        {
            "an explicit property to another explicit one", """
            public class Twice : IVal, IOther
            {
                int IVal.Value => 1;
                int IOther.Value => ((IVal)this).Value;
            }
            """, "value", 1, false
        },
        {
            "an explicit one passing its parameters through", """
            public class Passing : ITake, IGive
            {
                string ITake.Take(int x, string y) => y + x;
                string IGive.Take(int x, string y) => ((ITake)this).Take(x, y);
            }
            """, "take", 1, false
        },
        {
            "an explicit one through a chain of hand-offs", """
            public class Chain : IFoo, IBar
            {
                public string Run() => ((IFoo)this).Run();
                string IFoo.Run() => "foo";
                string IBar.Run() => Run();
            }
            """, "run", 1, true
        },
        {
            "an explicit one to another that hands off under another name, and so holds the name", """
            public class Slot : IFoo, IBar
            {
                string IFoo.Run() => Other();
                string IBar.Run() => ((IFoo)this).Run();
                private string Other() => "x";
            }
            """, "run", 1, false
        },
        {
            "an explicit one to a base's method", """
            public class Base { public string Run() => "base"; }
            public class Derived : Base, IFoo { string IFoo.Run() => Run(); }
            """, "run", 0, false
        },
    };

    /// <summary>
    /// An explicit implementation that hands back what the member holding its name answers is not written:
    /// that member answers every call through each interface, as they answer alike in .NET. Written, the
    /// later of two replaced the other and, handing off to it, called itself, and a method of the type's own
    /// it handed off to moved aside, refused where it answered another interface. The twin declares the
    /// name <paramref name="written"/> times: once where the type holds it, and never where a base does. A
    /// method of the type's own that hands off in turn to an explicit one with an answer of its own still
    /// moves aside (<paramref name="aside"/>), the explicit one holding the name.
    /// </summary>
    [Theory]
    [MemberData(nameof(HandOffs))]
    public void AnExplicitOneThatHandsOff_IsNotWritten(string why, string source, string name, int written, bool aside)
    {
        var typeName = Regex.Matches(source, @"public class (\w+)").Last().Groups[1].Value;
        var result = Compile(source, typeName);

        result.Errors.Should().BeEmpty(why);
        Declared(result.TypeScript, name).Should().Be(written, why);
        Declared(result.TypeScript, $"{name}$1").Should().Be(aside ? 1 : 0, why);
    }

    /// <summary>A method of the type's own beside an explicit one that hands off to ANOTHER member keeps
    /// moving aside (#708), and is not refused as answering another interface: the explicit one is written,
    /// and the method holds a name of its own.</summary>
    [Fact]
    public void AMethodBesideAnExplicitOneThatHandsOffElsewhere_HoldsANameOfItsOwn()
    {
        var result = Compile("""
            public class Elsewhere : IFoo
            {
                public string Run() => "own";
                string IFoo.Run() => Other();
                private string Other() => "other";
            }
            """, "Elsewhere");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("run$1()").And.Contain("return this.other();");
    }

    public static TheoryData<string, string, string, string> Refused => new()
    {
        {
            "two interfaces' members of one name", """
            public class Both : IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => "bar";
            }
            """, "Both", "'Both' implements 'IFoo.Run()' and 'IBar.Run()' explicitly, and both lower to `run()`"
        },
        {
            "a hand-off that passes something else", """
            public class Shifted : ITake, IGive
            {
                string ITake.Take(int x, string y) => y + x;
                string IGive.Take(int x, string y) => ((ITake)this).Take(x + 1, y);
            }
            """, "Shifted", "string IGive.Take(int x, string y) => ((ITake)this).Take(x, y);"
        },
        {
            "one interface over two type arguments", """
            public class Twice : IGet<int>, IGet<string>
            {
                string IGet<int>.Get() => "int";
                string IGet<string>.Get() => "string";
            }
            """, "Twice", "'IGet<int>.Get()' and 'IGet<string>.Get()'"
        },
        {
            "two explicit properties", """
            public class Props : IVal, IOther
            {
                int IVal.Value => 1;
                int IOther.Value => 2;
            }
            """, "Props", "`int IOther.Value => ((IVal)this).Value;`"
        },
        {
            "an explicit property beside the type's own", """
            public class Own : IVal
            {
                public int Value => 5;
                int IVal.Value => 1;
            }
            """, "Own", "'Own.Value' lowers to `value`, and so does the explicit implementation 'IVal.Value'"
        },
        {
            "an explicit one beside an override, which holds its base's slot", """
            public abstract class Abs { public abstract string Run(); }
            public class Conc : Abs, IFoo
            {
                public override string Run() => "own";
                string IFoo.Run() => "foo";
            }
            """, "Conc", "'Conc.Run()' lowers to `run()`"
        },
        {
            "an explicit one on the name of a base's method", """
            public class Base { public string Run() => "base"; }
            public class Derived : Base, IFoo { string IFoo.Run() => "foo"; }
            """, "Derived", "every call to 'Base.Run()' on a 'Derived'"
        },
        {
            "an explicit one on the name of a base's explicit one of another interface", """
            public class BaseX : IBar { string IBar.Run() => "bar"; }
            public class DerivedX : BaseX, IFoo { string IFoo.Run() => "foo"; }
            """, "DerivedX", "the explicit implementation of 'IBar.Run()' it inherits from 'BaseX'"
        },
        {
            "two static abstract members of one name", """
            public interface IMake2 { static abstract string Make(); }
            public class Maker : IMake, IMake2
            {
                static string IMake.Make() => "a";
                static string IMake2.Make() => "b";
            }
            """, "Maker", "both lower to `static make()`"
        },
        {
            "a record struct", """
            public record struct Pt(int X) : IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => "bar";
            }
            """, "Pt", "'Pt' implements 'IFoo.Run()' and 'IBar.Run()' explicitly"
        },
    };

    /// <summary>
    /// What the twin would write twice on one name along its chain is refused, where the browser kept one
    /// in silence: a call through one interface reached the other's answer, and a call bound to a base's
    /// member reached the explicit one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Refused))]
    public void WhatTheTwinWouldHoldTwice_IsRefused(string why, string source, string name, string said)
    {
        var result = Compile(source, name);

        var error = result.Errors.Should().ContainSingle(why).Subject;
        error.Code.Should().Be("EQ1007", why);
        error.Message.Should().Contain(said, why);
    }

    /// <summary>
    /// A call through <c>base</c> is no hand-off: it skips the type's override, which a call through the
    /// interface would reach on the twin, so the explicit one is written, and refused beside the override.
    /// A static one that calls another type's static of its name is written too: the twin of the type it
    /// answers for would hold nothing under the name.
    /// </summary>
    [Fact]
    public void WhatACallThroughTheInterfaceWouldNotReach_IsNoHandOff()
    {
        var based = Compile("""
            public class B { public virtual string Run() => "b"; }
            public class T : B, IFoo
            {
                public override string Run() => "t";
                string IFoo.Run() => base.Run();
            }
            """, "T");
        based.Errors.Should().ContainSingle().Which.Message.Should().Contain("'T.Run()' lowers to `run()`");

        var elsewhere = Compile("""
            public static class Helper { public static string Make() => "h"; }
            public class Maker : IMake { static string IMake.Make() => Helper.Make(); }
            """, "Maker");
        elsewhere.Errors.Should().BeEmpty();
        Declared(elsewhere.TypeScript, "make").Should().Be(1);
    }

    /// <summary>The hand-off a refusal proposes for a property with a setter writes both accessors, as the
    /// interface asks; an explicit event, which no twin holds (#746), meets nothing.</summary>
    [Fact]
    public void TheProposedHandOff_IsOneCSharpTakes()
    {
        var result = Compile("""
            public interface ISet1 { int Value { get; set; } }
            public interface ISet2 { int Value { get; set; } }
            public class Settable : ISet1, ISet2
            {
                private int a, b;
                int ISet1.Value { get => a; set => a = value; }
                int ISet2.Value { get => b; set => b = value; }
            }
            """, "Settable");
        result.Errors.Should().ContainSingle().Which.Message.Should()
            .Contain("`int ISet2.Value { get => ((ISet1)this).Value; set => ((ISet1)this).Value = value; }`");

        var evented = Compile("""
            public interface IHas { event System.Action Run; }
            public class Evented : IHas, IFoo
            {
                event System.Action IHas.Run { add { } remove { } }
                string IFoo.Run() => "foo";
            }
            """, "Evented");
        evented.Errors.Should().BeEmpty();
    }

    /// <summary>A typed <c>Equals</c> holds a name of its own, which the twin's <c>static $equatable</c> maps
    /// its contract to (#947), so two explicit ones over two types meet nothing.</summary>
    [Fact]
    public void TwoExplicitTypedEquals_HoldNamesOfTheirOwn()
    {
        var result = Compile("""
            public class TwoWays : System.IEquatable<int>, System.IEquatable<string>
            {
                bool System.IEquatable<int>.Equals(int n) => n == 1;
                bool System.IEquatable<string>.Equals(string? s) => s == "x";
            }
            """, "TwoWays");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("equals$1(").And.Contain("equals$2(");
    }

    public static TheoryData<string, string, string, string> Kept => new()
    {
        {
            "a hand-off through an overridable method, which a derived type may answer otherwise", """
            public class Via : IFoo
            {
                public string Run() => "own";
                public virtual string Other() => Run();
                string IFoo.Run() => Other();
            }
            """, "Via", "return this.other();"
        },
        {
            "a hand-off widening a number to a long, which the browser holds as a BigInt", """
            public class Wide : ILong
            {
                public int Run() => 1;
                long ILong.Run() => Run();
            }
            """, "Wide", "run$1()"
        },
        {
            "a hand-off boxing a struct, which copies it", """
            public struct Spot { public int X; }
            public class Boxer : IObj
            {
                private Spot saved;
                public Spot Run() => saved;
                object IObj.Run() => Run();
            }
            """, "Boxer", "run$1()"
        },
        {
            "a member kept on the server, which no twin holds", """
            public class Split : IFoo, IBar
            {
                [eQuantic.UI.Primitives.ServerOnly] string IFoo.Run() => "server";
                string IBar.Run() => "client";
            }
            """, "Split", "return 'client';"
        },
        {
            "a hand-off of an out, whose results come back under the names of the method that wrote them", """
            public class Outs : IOut
            {
                public void Run(out int value) { value = 42; }
                void IOut.Run(out int result) => Run(out result);
            }
            """, "Outs", "run$1()"
        },
        {
            "a hand-off of an array to a params parameter, which takes it as one element", """
            public class Counted : ICount
            {
                public int Run(params int[] xs) => xs.Length;
                int ICount.Run(int[] xs) => Run(xs);
            }
            """, "Counted", "run$1("
        },
        {
            "an async body, which completes its own task", """
            public class Tasker : ITask
            {
                public async System.Threading.Tasks.Task Run() { await System.Threading.Tasks.Task.Yield(); throw new System.InvalidOperationException(); }
                async System.Threading.Tasks.Task ITask.Run() { Run(); }
            }
            """, "Tasker", "run$1()"
        },
        {
            "a hand-off through an as, which may answer null", """
            public class Caster : IMaybe
            {
                public object Run() => 5;
                string? IMaybe.Run() => Run() as string;
            }
            """, "Caster", "run$1()"
        },
    };

    /// <summary>
    /// What a call through the interface would not reach the same way is no hand-off (Copilot's review of
    /// #1139): a member passed over on the way that a derived type may override, a value the hand-off widens
    /// or boxes into a copy. The explicit one is written, and a method of the type's own beside it moves
    /// aside. An explicit implementation kept on the server is in no twin, so it meets nothing there.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kept))]
    public void WhatACallThroughTheInterfaceWouldReachOtherwise_IsWritten(string why, string source, string name, string written)
    {
        var result = Compile(source, name);

        result.Errors.Should().BeEmpty(why);
        result.TypeScript.Should().Contain(written, why);
        Declared(result.TypeScript, "run").Should().Be(1, why);
    }

    public static TheoryData<string, string, string, string> RefusedAcrossTheChain => new()
    {
        {
            "a base's explicit one of the same interface over another type argument", """
            public class GetBase : IGet<int> { string IGet<int>.Get() => "int"; }
            public class GetDerived : GetBase, IGet<string> { string IGet<string>.Get() => "string"; }
            """, "GetDerived", "the explicit implementation of 'IGet<int>.Get()' it inherits from 'GetBase'"
        },
        {
            "an inherited abstract method, whose overrides fill its name", """
            public abstract class AbsBase { public abstract string Run(); }
            public abstract class Mid : AbsBase, IFoo { string IFoo.Run() => "interface"; }
            """, "Mid", "and so does 'AbsBase.Run()', which it inherits"
        },
        {
            "an explicit one on the name the runtime reads for object's Equals", """
            public class Probe : IProbe { bool IProbe.Equals(object other) => Equals(other); }
            """, "Probe", "the name the runtime reads for `Equals(object)`"
        },
        {
            "an explicit property on the name the runtime reads for object's ToString", """
            public class Texted : IText { string IText.ToString => "text"; }
            """, "Texted", "the name the runtime reads for `ToString()`"
        },
        {
            "an explicit static beside a static field of its name, which stays in its slot", """
            public interface IMake3 { static abstract string Make(); }
            public class Made : IMake3
            {
                public static string Make = "field";
                static string IMake3.Make() => "interface";
            }
            """, "Made", "'Made.Make' lowers to `static make()`"
        },
        {
            "two explicit ones that hand off to each other", """
            public class Loop : IFoo, IBar
            {
                string IFoo.Run() => ((IBar)this).Run();
                string IBar.Run() => ((IFoo)this).Run();
            }
            """, "Loop", "'Loop' implements 'IFoo.Run()' and 'IBar.Run()' explicitly"
        },
        {
            "a hand-off through this boxed to an interface, which calls a copy", """
            public struct Counter : IFoo, IBar
            {
                private int n;
                string IBar.Run() => (++n).ToString();
                string IFoo.Run() => ((IBar)this).Run();
            }
            """, "Counter", "'Counter' implements 'IBar.Run()' and 'IFoo.Run()' explicitly"
        },
    };

    /// <summary>A base's explicit implementation is judged from its definition, whichever construction was
    /// settled first, and a re-implementation is the same interface member over the same type arguments;
    /// a struct's hand-off through <c>this</c> boxed to an interface calls a copy, and is no hand-off.</summary>
    [Theory]
    [MemberData(nameof(RefusedAcrossTheChain))]
    public void WhatTheChainWouldHoldTwice_IsRefused(string why, string source, string name, string said)
    {
        var result = Compile(source, name);

        var error = result.Errors.Should().ContainSingle(why).Subject;
        error.Code.Should().Be("EQ1007", why);
        error.Message.Should().Contain(said, why);
    }

    /// <summary>
    /// A generic base is settled once, from its definition, whichever construction asks first (Copilot's review
    /// of #1139). Asking the name of the base's own <c>Run()</c>, which the check of a derived method does, settled
    /// the definition, and the base's explicit one, taken from the construction, was then found among none of
    /// the definition's members, so its collision passed.
    /// </summary>
    [Fact]
    public void AGenericBaseSettledFromItsDefinition_IsJudgedAsItsConstruction()
    {
        var result = Compile("""
            public class GenBase<T> : IBar
            {
                public string Run() => "own";
                string IBar.Run() => "bar";
            }
            public class OnInt : GenBase<int>, IFoo
            {
                public string Hello() => "hi";
                string IFoo.Run() => "foo";
            }
            """, "OnInt");

        result.Errors.Should().ContainSingle().Which.Message.Should()
            .Contain("the explicit implementation of 'IBar.Run()' it inherits from 'GenBase'");
    }

    /// <summary>A member kept on the server is where a hand-off ends: its body never runs in the browser, so the
    /// explicit implementation that calls it is written, and an app's build refuses the call there (EQ2010),
    /// where following the body left the explicit one out and the refusal with it (Copilot's third review of
    /// #1139). Here, with no reference for the attribute to bind to, the call is written.</summary>
    [Fact]
    public void AMemberKeptOnTheServer_IsWhereAHandOffEnds()
    {
        var result = Compile("""
            public class Hidden : IFoo
            {
                string IFoo.Run() => Secret();
                [eQuantic.UI.Primitives.ServerOnly] string Secret() => Run();
                public string Run() => "local";
            }
            """, "Hidden");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("return this.secret();");
        Declared(result.TypeScript, "run").Should().Be(1);
    }

    /// <summary>A component's server action is a stub in the browser, whose body runs on the server, so an
    /// explicit implementation that calls it hands off to the stub and goes no further (Copilot's second
    /// review of #1139).</summary>
    [Fact]
    public void AServerActionIsWhereAHandOffEnds()
    {
        var result = new ComponentCompiler().CompileSource("""
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;
            public interface IAsync { Task<string> Run(); }
            public sealed class Fetcher : StatefulComponent, IAsync
            {
                Task<string> IAsync.Run() => Fetch();
                [ServerAction] public Task<string> Fetch() => Run();
                public Task<string> Run() => Task.FromResult("local");
                public override VisualNode Build(ComponentContext context) => new Text("t");
            }
            """, "Probe.cs").Single(result => result.ComponentName == "Fetcher");

        result.Errors.Should().BeEmpty();
        Declared(result.TypeScript, "run").Should().Be(1);
        result.TypeScript.Should().Contain("this.fetch()");
    }

    /// <summary>A component's explicit property beside its own of the name, before or after it, is kept and
    /// left out where it hands off: the parser dropped the second, and the getter that remained read
    /// itself.</summary>
    [Theory]
    [InlineData("int IVal.Value => Value; public int Value => 5;")]
    [InlineData("public int Value => 5; int IVal.Value => Value;")]
    public void AComponentsExplicitPropertyThatHandsOff_LeavesItsOwn(string members)
    {
        var result = new ComponentCompiler().CompileSource($$"""
            using eQuantic.UI.Core;
            using eQuantic.UI.Components;
            public interface IVal { int Value { get; } }
            public class Gauge : StatelessComponent, IVal
            {
                {{members}}
                public override IComponent Build(RenderContext context) => new Text(((IVal)this).Value.ToString());
            }
            """, "Probe.cs").Single(result => result.ComponentName == "Gauge");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("return 5;");
        Declared(result.TypeScript, "value").Should().Be(1);
    }

    /// <summary>The enumeration interfaces' contract reads a hand-off as it did: two explicit
    /// <c>GetEnumerator()</c>s over one generic helper answer alike, whatever type argument they give it.</summary>
    [Fact]
    public void TwoEnumeratorsOverOneGenericHelper_AnswerAlike()
    {
        var result = Compile("""
            public class Seq : System.Collections.Generic.IEnumerable<int>
            {
                System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator() => Make<int>();
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Make<int>();
                private System.Collections.Generic.IEnumerator<int> Make<T>() { yield return 1; }
            }
            """, "Seq");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>The error points at the later of the two, as every EQ1007 does.</summary>
    [Fact]
    public void TheErrorPointsAtTheSecond()
    {
        var result = Compile("""
            public class Both : IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => "bar";
            }
            """, "Both");

        var error = result.Errors.Should().ContainSingle().Subject;
        error.Line.Should().Be(Faces.Split('\n').Length + 4);
        error.Message.Should().Contain("`string IBar.Run() => ((IFoo)this).Run();`");
    }

    /// <summary>A derived class that implements an interface member again, explicitly, beside its base's
    /// explicit implementation of the same member, re-implements it: C# reaches the derived one on a
    /// derived instance and the base's on a base instance, as the twin's chain does.</summary>
    [Fact]
    public void ABaseExplicitImplementationOfTheSameMember_IsReimplemented()
    {
        var result = Compile("""
            public class First : IFoo { string IFoo.Run() => "first"; }
            public class Again : First, IFoo { string IFoo.Run() => "again"; }
            """, "Again");

        result.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// A component's method beside an explicit implementation of its name holds a name of its own (#708),
    /// and the explicit one the interface member's. The component's parser took the second for an overload
    /// and dropped it, so the twin called a method nobody wrote, whichever came first.
    /// </summary>
    [Theory]
    [InlineData("public string Name() => \"own\"; string INamedThing.Name() => \"interface\";")]
    [InlineData("string INamedThing.Name() => \"interface\"; public string Name() => \"own\";")]
    public void AComponentsMethodBesideAnExplicitOne_IsWrittenWithIt(string members)
    {
        var result = new ComponentCompiler().CompileSource($$"""
            using eQuantic.UI.Core;
            using eQuantic.UI.Components;
            public interface INamedThing { string Name(); }
            public class Card : StatelessComponent, INamedThing
            {
                {{members}}
                public override IComponent Build(RenderContext context) => new Text(Name() + ((INamedThing)this).Name());
            }
            """, "Probe.cs").Single(result => result.ComponentName == "Card");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("this.name$1() + this.name()");
        Declared(result.TypeScript, "name$1").Should().Be(1);
        Declared(result.TypeScript, "name").Should().Be(1);
    }

    /// <summary>A component's explicit implementation that hands off is not written either, and one of
    /// its own beside another interface's of one name is refused.</summary>
    [Fact]
    public void AComponentsExplicitOnes_AreSettledAsAClasssAre()
    {
        var results = new ComponentCompiler().CompileSource("""
            using eQuantic.UI.Core;
            using eQuantic.UI.Components;
            public interface IFoo { string Run(); }
            public interface IBar { string Run(); }
            public class Forwarding : StatelessComponent, IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => ((IFoo)this).Run();
                public override IComponent Build(RenderContext context) => new Text(((IBar)this).Run());
            }
            public class Apart : StatelessComponent, IFoo, IBar
            {
                string IFoo.Run() => "foo";
                string IBar.Run() => "bar";
                public override IComponent Build(RenderContext context) => new Text(((IBar)this).Run());
            }
            """, "Probe.cs").ToList();

        var forwarding = results.Single(result => result.ComponentName == "Forwarding");
        forwarding.Errors.Should().BeEmpty();
        Declared(forwarding.TypeScript, "run").Should().Be(1);
        forwarding.TypeScript.Should().Contain("return 'foo';");
        results.Single(result => result.ComponentName == "Apart").Errors.Should().ContainSingle()
            .Which.Code.Should().Be("EQ1007");
    }
}
