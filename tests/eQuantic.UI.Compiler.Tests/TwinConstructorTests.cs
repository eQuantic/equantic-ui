using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A record's or a struct's twin has one JavaScript constructor, a branch per C# constructor on how
/// many arguments arrive (#413): each one that does its own work binds its parameters, sets the members
/// and runs its body, and each one that chains with `: this(…)` runs its root and then its own body. A
/// constructor the twin cannot tell apart that way, by a count of arguments another takes, or one that
/// chains to a constructor that chains in turn, is refused (EQ1009): every explicit constructor of a
/// record was dropped before, in silence, and `new ExplicitCtor(3)` set its member to 3 where C# ran
/// the body. A record's copy constructor is no branch: `new` never reaches it.
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
            // Two constructors doing their own work, with one count of arguments.
            "Twins", """
            public record Twins
            {
                public int A { get; init; }
                public Twins(int a) { A = a; }
                public Twins(string s) { A = s.Length; }
            }
            """, "Twins(string s)"
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
        {
            // Two alternates of one count: the twin cannot tell which one a call means either.
            "Pair", """
            public record Pair(int A, int B)
            {
                public Pair(int a) : this(a, a) { }
                public Pair(string s) : this(s.Length, 0) { }
            }
            """, "Pair(string s)"
        },
        {
            // An alternate's optional parameter reaches a count the main constructor takes.
            "Reach", "public record Reach(int A, int B) { public Reach(string s, int n = 0) : this(s.Length, n) { } }",
            "Reach(string s, int n = 0)"
        },
        {
            // A struct's alternate that chains to its implicit constructor, beside an explicit one
            // that is the twin's: it would run the explicit one's body, which C# does not run.
            "Odd", """
            public struct Odd
            {
                public int A;
                public Odd(int a) { A = a; }
                public Odd(int a, int b) : this() { A = b; }
            }
            """, "Odd(int a, int b)"
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
        // The alternate's own parameters live in a block of their own, and the chain's arguments cross it
        // in temporaries: no function holds the C#.
        result.TypeScript.Should().Contain(
            "if (arguments.length === 1) { let $c0: any, $c1: any; { let at: any = arguments[0]; $c0 = at; $c1 = at + 1; } start = $c0; end = $c1; }");
    }

    [Fact]
    public void AnAlternateWithAnOptionalParameter_IsReachedByEachCountItTakes()
    {
        var result = Compile("public record Box(int A, int B, int C) { public Box(int a, int b = 5) : this(a, b, 0) { } }", "Box");

        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => error.Message)));
        result.TypeScript.Should().Contain(
            "if (arguments.length >= 1 && arguments.length <= 2) { let $c0: any, $c1: any, $c2: any; { let a: any = arguments[0], b: any = arguments[1] === undefined ? 5 : arguments[1]; "
            + "$c0 = a; $c1 = b; $c2 = 0; } a = $c0; b = $c1; c = $c2; }");
    }

    [Fact]
    public void ConstructorsWithBodiesOfTheirOwn_AreEachABranch()
    {
        var result = Compile("""
            public struct Money
            {
                public decimal A; public string C;
                public Money(decimal a) { A = a; C = "EUR"; }
                public Money(decimal a, string c) { A = a; C = c; }
            }
            """, "Money");

        result.Success.Should().BeTrue(string.Join("\n", result.Errors.Select(error => error.Message)));
        result.TypeScript.Should().Contain("constructor(...$a: any[]) { let a: any, c: any; let $k: any = -1; "
            + "if ($a.length === 1) { a = $a[0]; $k = 0; } else if ($a.length === 2) { a = $a[0]; c = $a[1]; $k = 1; } ");
        result.TypeScript.Should().Contain("if ($k === 0) { this.a = a; this.c = 'EUR'; } if ($k === 1) { this.a = a; this.c = c; } ");
        // C#'s null for a string member TypeScript declares never null: strict TypeScript refuses a bare
        // `null` there, and the runtime's own twins are compiled strict.
        result.TypeScript.Should().Contain("declare c: string;").And.Contain("this.c = null!;");
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

    /// <summary>
    /// A record's own copy constructor is what `with` copies through, and never a branch `new` reaches:
    /// taken for a second constructor of one argument, it refused the type (EQ1009), which compiled
    /// before.
    /// </summary>
    [Fact]
    public void ACopyConstructor_IsNoBranch_AndIsNotRefused()
    {
        var result = Compile("""
            public record Doc
            {
                public int Size;
                public Doc(int capacity) { Size = capacity; }
                protected Doc(Doc original) { Size = original.Size + 1; }
            }
            """, "Doc");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("constructor(capacity: any)").And.NotContain("original");
    }
}
