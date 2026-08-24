using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A record's STATIC members belong to the twin, and its consts do not belong to its value.
/// <para>
/// Both defects were silent in the worst way: the build stayed green, the server rendered the page
/// correctly, and only a browser showed the difference. They are two rules about "what is a value
/// member" that failed in opposite directions — the static property let too little through, the
/// const let too much.
/// </para>
/// </summary>
public class RecordStaticMemberTests
{
    private const string Page = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed record Chrome(string Tag)
        {
            public static Chrome Default { get; } = new("well-known");
            public static Chrome Mutable { get; set; } = new("also-well-known");
            public static readonly Chrome Field = new("by-field");
            public const string Marker = "a-const";
            public static string Describe() => "described";
        }

        [Page("/chrome")]
        public sealed class ChromePage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text(Chrome.Default.Tag, TypeRole.BodyM);
        }
        """;

    private static string Twin() => new ComponentCompiler().CompileSource(Page, "Chrome.cs")
        .Single(r => r.ComponentName == "Chrome").TypeScript;

    /// <summary>A record whose only DECLARED data is a const — no positional parameters, no
    /// instance members. It is still a type the app names, and it still needs its twin.</summary>
    private const string ConstOnly = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed record Limits
        {
            public const int MaxRows = 1000;
            public static string Describe() => "limits";
        }

        [Page("/limits")]
        public sealed class LimitsPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text(Limits.Describe(), TypeRole.BodyM);
        }
        """;

    [Fact]
    public void ARecordWhoseOnlyDataIsAConstStillGetsATwin()
    {
        // Discovery must not be decided by the INSTANCE-value list. Excluding the const from the
        // record's value is right; letting that exclusion also delete the type is not — the page
        // calls Limits.describe() and would reference a module nobody emitted.
        var twin = new ComponentCompiler().CompileSource(ConstOnly, "Limits.cs")
            .Single(r => r.ComponentName == "Limits").TypeScript;

        twin.Should().Contain("static maxRows = 1000").And.Contain("static describe()");
    }

    [Fact]
    public void AStaticPropertyWithAnInitializerReachesTheTwin()
    {
        // `static T P { get; } = new(…)` is an auto-property: no getter body, so the emitter's
        // property loop — which only knew `=> …`, `get => …` and `get { … }` — skipped it entirely.
        // The call site still wrote `Chrome.default.tag`, so the page threw on hydration having
        // rendered perfectly on the server.
        Twin().Should().Contain("static default = new Chrome('well-known')");
    }

    [Fact]
    public void ASettableStaticPropertyReachesItToo()
    {
        // Same shape with a setter. Nothing about `{ get; set; }` makes it less of a static value.
        Twin().Should().Contain("static mutable = new Chrome('also-well-known')");
    }

    [Fact]
    public void TheOtherStaticShapesStillReachIt()
    {
        var twin = Twin();
        twin.Should().Contain("static field = new Chrome('by-field')");
        twin.Should().Contain("static marker = 'a-const'");
        twin.Should().Contain("static describe()");
    }

    [Fact]
    public void AConstIsNotPartOfTheRecordsVALUE()
    {
        var twin = Twin();

        // A const is static state. C# gives `record Chrome(string Tag)` ONE positional parameter and
        // prints one member; counting the const as a value member gave the twin two, compared it in
        // equals, offered it to `with`, and printed `Marker = undefined` in toString. A record that
        // crosses in a payload then disagreed with the server about its own text.
        // ONE positional parameter, exactly as C# declares it.
        twin.Should().Contain("constructor(tag: any = null)").And.NotContain("marker: any = null");
        // …and the const is nowhere in the value semantics: not compared, not patchable, not printed.
        twin.Should().NotContain("o.marker").And.NotContain("'marker' in patch").And.NotContain("Marker = ");
        // It is still a static, which is what it always was.
        twin.Should().Contain("static marker = 'a-const'");
    }

    private const string Shapes = """
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        public sealed record Shapes(string Tag)
        {
            public static int First { get; } = Second;
            public static readonly int Second = 7;
            public static int Counted { get; set; }
            public static int Half { get; set => field = value / 2; }
        }

        [Page("/shapes")]
        public sealed class ShapesPage : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
                => new Text(Shapes.First.ToString(), TypeRole.BodyM);
        }
        """;

    private static string ShapesTwin() => new ComponentCompiler().CompileSource(Shapes, "Shapes.cs")
        .Single(r => r.ComponentName == "Shapes").TypeScript;

    [Fact]
    public void StaticInitialisersKeepTheirDECLARATIONOrder()
    {
        var twin = ShapesTwin();

        // .NET runs static initialisers top to bottom, so `First = Second` written ABOVE
        // `Second = 7` reads Second's default and not 7. Emitting all fields and then all
        // properties reversed that for every type whose source interleaves them.
        twin.IndexOf("static first", StringComparison.Ordinal)
            .Should().BeLessThan(twin.IndexOf("static second", StringComparison.Ordinal),
                "the twin must run them in the order the source declares");
    }

    [Fact]
    public void AStaticPropertyWithoutAnInitialiserTakesItsTypesDefault()
    {
        // C# gives `static int Counted { get; set; }` a 0. `undefined` is a different number to
        // every reader of it, and the server would have said 0.
        ShapesTwin().Should().Contain("static counted = 0");
    }

    [Fact]
    public void APropertyWithACustomSetterIsNotFlattenedIntoAField()
    {
        // `{ get; set => … }` is BEHAVIOUR. Emitting it as a plain static field would keep the name
        // and silently drop the halving — the worst kind of wrong, because it looks like it works.
        ShapesTwin().Should().NotContain("static half = ");
    }

    [Fact]
    public void ARecordWhoseOnlySurfaceIsAnOperatorStillGetsATwin()
    {
        const string source = """
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public sealed record Money
            {
                public static Money operator +(Money a, Money b) => a;
                public static implicit operator Money(int v) => new();
            }

            [Page("/money")]
            public sealed class MoneyPage : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                    => new Text("x", TypeRole.BodyM);
            }
            """;

        // Emit writes an operator as a static method and a call site lowers to it, so a type whose
        // only surface is one still needs its twin. Discovery has to know every shape Emit writes,
        // or it deletes a type the emitted code goes on referencing.
        new ComponentCompiler().CompileSource(source, "Money.cs")
            .Should().Contain(r => r.ComponentName == "Money");
    }

    [Fact]
    public void AStaticCharOrEnumTakesTheDefaultDOTNETGivesIt()
    {
        const string source = """
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public enum Kind { Low, High }

            public sealed record Marks(string Tag)
            {
                public static char Sep { get; set; }
                public static Kind Level { get; set; }
            }

            [Page("/marks")]
            public sealed class MarksPage : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                    => new Text(Marks.Sep.ToString(), TypeRole.BodyM);
            }
            """;

        var twin = new ComponentCompiler().CompileSource(source, "Marks.cs")
            .Single(r => r.ComponentName == "Marks").TypeScript;

        // A syntax-only default cannot see through a NAME: it answers null for both, where .NET
        // gives '\0' and the zero-valued member. The symbol can, and the server would have said so.
        twin.Should().Contain(@"static sep = '\0'");
        twin.Should().Contain("static level = 'low'");
    }

    [Fact]
    public void AnOperatorEmitCannotWriteDoesNotConjureATwin()
    {
        const string source = """
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            public sealed record Flags
            {
                public static Flags operator &(Flags a, Flags b) => a;
            }

            [Page("/flags")]
            public sealed class FlagsPage : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                    => new Text("x", TypeRole.BodyM);
            }
            """;

        // Emit has no method name for `&`, so it writes nothing for this operator and no call site
        // can lower it either. Discovery must answer the same question Emit does — saying yes here
        // would emit an empty module standing in for an operator nobody can use.
        new ComponentCompiler().CompileSource(source, "Flags.cs")
            .Should().NotContain(r => r.ComponentName == "Flags");
    }
}
