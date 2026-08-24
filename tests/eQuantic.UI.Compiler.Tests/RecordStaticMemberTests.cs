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
}
