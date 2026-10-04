using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A record's or a struct's twin has ONE constructor, the C# constructor the twin IS (#413): its
/// primary one, or the one explicit constructor that runs a body of its own. The others reach it by
/// how many arguments arrive. A constructor the twin cannot tell apart that way, or that runs a body
/// of its own beside the main one, is refused (EQ1009): every explicit constructor of a record was
/// dropped before, in silence, and `new ExplicitCtor(3)` set its member to 3 where C# ran the body.
/// An indexer takes two names on its twin, so a second one, or a method on either name, is EQ1007.
/// </summary>
public class TwinConstructorTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    public static TheoryData<string, string, string> Refused => new()
    {
        {
            // Two constructors of one arity: the twin cannot tell which one a call means.
            "Coded", "public record Coded(int Code) { public Coded(string text) : this(text.Length) { } }", "Coded(string text)"
        },
        {
            // Two constructors each running a body of its own.
            "Twice", """
            public record Twice
            {
                public int A { get; init; }
                public Twice() { A = 1; }
                public Twice(int a) { A = a; }
            }
            """, "Twice()"
        },
        {
            // An alternate that chains to another alternate.
            "Hop", """
            public record Hop(int A, int B)
            {
                public Hop(int a, int b, int c) : this(a + b + c, 0) { }
                public Hop() : this(1, 2, 3) { }
            }
            """, "Hop()"
        },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void AConstructorTheTwinCannotReach_IsRefused(string name, string source, string named)
    {
        var result = Compile(source, name);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "EQ1009" && error.Message.Contains(named),
            "the refusal names the constructor the twin cannot reach");
    }

    [Fact]
    public void AnAlternateThatChainsToTheMainOne_IsTheTwinsBranch()
    {
        var result = Compile("public record Span(int Start, int End) { public Span(int at) : this(at, at + 1) { } }", "Span");

        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => error.Message)));
        result.TypeScript.Should().Contain("if (arguments.length === 1) { [start, end] = ((at: any) => [at, at + 1])(arguments[0]); }");
    }

    [Fact]
    public void AnExplicitConstructor_IsTheTwinsConstructor()
    {
        var result = Compile("public record Doubled { public int A { get; init; } public Doubled(int a) { A = a * 2; } }", "Doubled");

        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => error.Message)));
        result.TypeScript.Should().Contain("constructor(a: any) { this.a = 0; this.a = a * 2; }");
    }

    public static TheoryData<string, string> Collisions => new()
    {
        {
            "Two", """
            public sealed class Two
            {
                public int this[int i] => i;
                public int this[string s] => s.Length;
            }
            """
        },
        {
            "Named", """
            public sealed record Named
            {
                public int this[int i] { get => i; set { } }
                public void SetItem(int i, int value) { }
            }
            """
        },
    };

    [Theory]
    [MemberData(nameof(Collisions))]
    public void AnIndexerShares_ItsTwinsNames_WithNothing(string name, string source)
    {
        var result = Compile(source, name);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "EQ1007" && error.Message.Contains("this["),
            "an indexer lowers to `item` and `setItem`, and a second member on either name would take its place");
    }
}
