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
        """;

    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(Faces + "\n" + source, "Probe.cs").Single(result => result.ComponentName == name);

    /// <summary>How many members of <paramref name="name"/> a twin declares: a method, a getter, a static.</summary>
    private static int Declared(string typeScript, string name) =>
        Regex.Matches(typeScript, $@"^\s*(static\s+)?(get\s+)?{Regex.Escape(name)}\(", RegexOptions.Multiline).Count;

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
