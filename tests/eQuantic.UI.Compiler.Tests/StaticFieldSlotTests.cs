using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A static field moves to <c>name$</c> where a static member of its type or of its chain holds its twin
/// name, as an instance field does (#396, #1175), and every declaration, initialization and access
/// follows it. The backing-field idiom, <c>private static int count;</c> beside <c>public static int
/// Count</c>, was refused (EQ1007): both lowered to <c>static count</c>, and the field, defined over the
/// accessor when the class was, would have answered every access to it.
/// </summary>
public class StaticFieldSlotTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    /// <summary>Each kind of twin, and each place a static is written: declared as a field, built in the
    /// type initializer's slots, read and written by a method and an accessor, inherited, and a constant.</summary>
    public static TheoryData<string, string, string[]> Moved => new()
    {
        {
            "Counter", "public static class Counter { private static int count; public static int Count { get => count; set => count = value * 2; } }",
            ["static count$: number = 0", "return Counter.count$", "Counter.count$ = value * 2", "static get count()"]
        },
        {
            "Seq", "public sealed class Seq { private static int next; public static int Next() => ++next; public static int Peek => next; }",
            ["static next$: number = 0", "++Seq.next$", "static next()"]
        },
        {
            "Ordered", "public sealed class Ordered { private static int count = Seed(); private static int Seed() => 5; public static int Count => count; }",
            ["$slots.count$ = Ordered.seed()", "static get count$()", "return Ordered.count$"]
        },
        {
            "Stamp", "public sealed record Stamp(int V) { private static int made; public static int Made => made; public static Stamp Make(int v) { made++; return new(v); } }",
            ["static made$ = 0", "Stamp.made$++", "return Stamp.made$"]
        },
        {
            "Spot", "public struct Spot { private static int made; public static int Made => made; }",
            ["static made$ = 0", "return Spot.made$"]
        },
        {
            "Limits", "public static class Limits { private const int max = 10; public static int Max => max * 2; }",
            ["static max$: number = 10", "static get max()"]
        },
        {
            "Tally", "public class Tally { protected static int total; } public sealed class Ledger : Tally { public static int Total() => ++total; }",
            ["static total$: number = 0"]
        },
        {
            "Ledger", "public class Tally { protected static int total; } public sealed class Ledger : Tally { public static int Total() => ++total; }",
            ["++Tally.total$", "static total()"]
        },
        {
            "Page", """
            using eQuantic.UI.Primitives;

            [Page("/probe")]
            public sealed class Page : StatelessComponent
            {
                private static int count;
                public static int Count => count;
                public override VisualNode Build(ComponentContext context) => new Text("" + Count, TypeRole.BodyM);
            }
            """,
            ["static count$: number = 0", "return Page.count$"]
        },
    };

    [Theory]
    [MemberData(nameof(Moved))]
    public void AStaticFieldBesideAStaticMemberOfItsName_MovesToASlotOfItsOwn(string name, string source, string[] written)
    {
        var result = Compile(source, name);

        result.Errors.Should().BeEmpty();
        foreach (var text in written) result.TypeScript.Should().Contain(text);
    }

    /// <summary>A static of a type the runtime provides keeps its name: the runtime's twin is written by a
    /// build that never reads an app's types derived from it, so moving it for one would read a slot the
    /// runtime does not hold.</summary>
    [Fact]
    public void AStaticOfARuntimeProvidedType_KeepsItsName()
    {
        var results = new ComponentCompiler().CompileSource("""
            [eQuantic.UI.Primitives.RuntimeProvided] public class Base { public static int count = 1; }
            public sealed class Derived : Base { public static int Count() => 4; }
            public sealed class Reader { public static int Read() => Base.count; }
            """, "Probe.cs").ToList();

        results.Single(result => result.ComponentName == "Reader").TypeScript
            .Should().Contain("Base.count;").And.NotContain("count$");
    }

    /// <summary>A static and an instance member never meet, one on the class and the other on its
    /// prototype, so neither moves the other.</summary>
    [Fact]
    public void AStaticField_MovesForNoInstanceMember()
    {
        var result = Compile("public sealed class Tally { private static int count; public int Count => count; }", "Tally");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("static count: number = 0").And.NotContain("count$");
    }
}
