using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A method declared twice under one name in one type is refused (EQ1007), on every emitter.
/// <para>
/// Each of them lost one of the two in silence, and each in its own way: a plain class, a record and
/// a static class wrote both, and JavaScript kept the last, while a component's parser kept the
/// first and dropped the other. The kinds are compared side by side because they drifted apart
/// once already, as <c>PlainClassOperatorTests</c> records.
/// </para>
/// </summary>
public class OverloadedMethodTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    public static TheoryData<string, string, int> Overloads => new()
    {
        {
            "Plain", """
            public sealed class Plain
            {
                public int Twice(int x) => x * 2;
                public string Twice(string s) => s + s;
            }
            """, 4
        },
        {
            "Rec", """
            public sealed record Rec(int X)
            {
                public int Plus(int y) => X + y;
                public int Plus(int y, int z) => X + y + z;
            }
            """, 4
        },
        {
            "Helpers", """
            using System.Collections.Generic;

            public static class Helpers
            {
                public static int Size(this string s) => s.Length;
                public static int Size(this List<int> l) => l.Count;
            }
            """, 6
        },
        {
            "Blocks", """
            using System.Collections.Generic;

            public static class Blocks
            {
                extension(string s)
                {
                    public int Size() => s.Length;
                }

                extension(List<int> l)
                {
                    public int Size() => l.Count;
                }
            }
            """, 12
        },
        {
            "Page", """
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private string Label(int x) => x.ToString();
                private string Label(string s) => s;
                public override VisualNode Build(ComponentContext context) => new Text(Label(1) + Label("a"), TypeRole.BodyM);
            }
            """, 7
        },
    };

    [Theory]
    [MemberData(nameof(Overloads))]
    public void AnOverloadIsRefused_OnEveryEmitter(string name, string source, int line)
    {
        var result = Compile(source, name);

        result.Success.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be("EQ1007");
        error.Line.Should().Be(line, "the SECOND declaration is the one the twin could not carry");
    }

    [Fact]
    public void TheErrorNamesBothOverloads()
    {
        var error = Compile("""
            public sealed class Plain
            {
                public int Twice(int x) => x * 2;
                public string Twice(string s) => s + s;
            }
            """, "Plain").Errors.Single();

        error.Message.Should().Contain("'Plain.Twice(string)' lowers to `twice()`")
            .And.Contain("'Twice(int)' (line 3)");
    }

    /// <summary>One lives on the class and the other on its prototype.</summary>
    [Fact]
    public void AStaticAndAnInstanceMethodMayShareAName()
    {
        var result = Compile("""
            public sealed class Plain
            {
                public static int Make(int x) => x;
                public int Make() => 1;
            }
            """, "Plain");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("static make(").And.Contain("\n    make(");
    }

    /// <summary>Two names C# keeps apart, and the lowering folds into one.</summary>
    [Fact]
    public void TwoNamesThatLowerToOne_AreRefused()
    {
        var result = Compile("""
            public sealed class Plain
            {
                public int Foo() => 1;
                public int foo() => 2;
            }
            """, "Plain");

        result.Errors.Should().ContainSingle().Which.Code.Should().Be("EQ1007");
    }

    /// <summary>The defining half and the implementing half are one method.</summary>
    [Fact]
    public void APartialMethodIsOneMethod()
    {
        var result = Compile("""
            public sealed partial class Plain
            {
                partial void Hook();
                partial void Hook() { }
                public void Run() => Hook();
            }
            """, "Plain");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>A component's server-only method never reaches its twin, so it names nothing there.</summary>
    [Fact]
    public void AComponentsServerOnlyMethod_TakesNoName()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                [ServerOnly] private string Label(System.Net.Http.HttpClient client) => "";
                private string Label(string s) => s;
                public override VisualNode Build(ComponentContext context) => new Text(Label("a"), TypeRole.BodyM);
            }
            """, "Page");

        result.Errors.Should().NotContain(error => error.Code == "EQ1007");
    }

    /// <summary>A static class nested in a component is embedded in its module, a class of its own.</summary>
    [Fact]
    public void AStaticClassNestedInAComponent_IsCheckedToo()
    {
        var result = Compile("""
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private static class Copy
                {
                    public static string Title(int count) => count.ToString();
                    public static string Title(string name) => name;
                }

                public override VisualNode Build(ComponentContext context) => new Text(Copy.Title(1), TypeRole.BodyM);
            }
            """, "Page");

        result.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Code = "EQ1007", Line = 9 });
    }
}
