using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A slot holding a COMPAT type — a long, a decimal, a date — is described in three places, and
/// they have to say the same thing: the TypeScript the twin declares, the entry in
/// <c>$hydration</c> that coerces an incoming payload, and the default the constructor writes when
/// the caller supplies none. Any two of them agreeing is not enough.
/// <para>
/// The site's home page died on the third. A `long` PROPERTY was declared `bigint`, but properties
/// were missing from the hydration map, so the payload's JSON number stayed a number — and the
/// implicit default answered plain `0` because the parser had its own copy of "default(T) on this
/// side" that lumped Int64 in with the numerics. Then `value / 1000n` threw "Cannot mix BigInt and
/// other types", in the browser only, after hydration, on a page the server had rendered perfectly.
/// </para>
/// </summary>
public class CompatSlotAgreementTests
{
    private const string Source = """
        using System;
        using eQuantic.UI.Primitives;

        [Component]
        public sealed class Stats : StatelessComponent
        {
            public long Downloads { get; init; }
            public decimal Revenue { get; init; }
            public DateTime Since { get; init; }
            public int Stars { get; init; }
            public string Label { get; init; } = "";
            private long _internal;

            public override VisualNode Build(ComponentContext context)
                => new Text(Label, TypeRole.BodyM, context.Theme.TextPrimary);
        }
        """;

    private static string Twin() => new ComponentCompiler().CompileSource(Source, "Stats.cs")
        .Single(result => result.ComponentName == "Stats").TypeScript;

    [Theory]
    [InlineData("downloads", "bigint", "long", "$eq.num.long(0)")]
    [InlineData("revenue", "Decimal", "decimal", "$eq.num.dec(0)")]
    [InlineData("since", "DateTime", "dateTime", "")]
    public void ACompatSlot_IsDeclared_Hydrated_AndDefaulted_TheSameWay(
        string slot, string declared, string spec, string @default)
    {
        var twin = Twin();

        twin.Should().MatchRegex($@"declare {slot}: {Regex.Escape(declared)}\b",
            "the declared type is what every use site compiles against");
        twin.Should().MatchRegex($@"\$hydration = \{{[^}}]*\b{slot}: '{spec}'",
            "a payload arrives as JSON — without an entry here the wire form never becomes the runtime one");
        // A DateTime has no zero on this side to write, so there is no default to check — the
        // absence is the honest answer and the empty expectation says so.
        if (@default.Length > 0)
        {
            twin.Should().Contain($"this.{slot} = {@default}",
                "an unset slot has to hold what C# would hold, not what JSON happens to spell");
        }
    }

    /// <summary>A date is a runtime class on this side; it hydrates by spec like the numbers do.</summary>
    [Fact]
    public void ADateProperty_IsInTheBoundaryToo()
    {
        Twin().Should().MatchRegex(@"\$hydration = \{[^}]*\bsince: 'dateTime'");
    }

    /// <summary>Nothing is emitted for the slots whose wire form IS their runtime form — the
    /// common case stays clean, which is why the map is worth having at all.</summary>
    [Fact]
    public void APlainSlot_IsNotInTheBoundary()
    {
        var map = Regex.Match(Twin(), @"\$hydration = \{[^}]*\}").Value;

        map.Should().NotContain("stars:").And.NotContain("label:");
    }

    /// <summary>A PRIVATE field was already covered; the property beside it was not, and that
    /// asymmetry is the whole bug. Both are slots a payload fills.</summary>
    [Fact]
    public void AFieldAndAPropertyOfTheSameTypeAreBothCovered()
    {
        var map = Regex.Match(Twin(), @"\$hydration = \{[^}]*\}").Value;

        map.Should().Contain("_internal: 'long'").And.Contain("downloads: 'long'");
    }

    /// <summary>A component with ONLY properties gets a boundary too. The map used to be emitted
    /// inside the "are there fields" branch, so the shape most sections take — props in, nothing
    /// private — was the one shape it never covered.</summary>
    [Fact]
    public void AComponentWithNoFieldsAtAll_StillGetsItsBoundary()
    {
        const string source = """
            using eQuantic.UI.Primitives;

            [Component]
            public sealed class Tally : StatelessComponent
            {
                public long Count { get; init; }

                public override VisualNode Build(ComponentContext context)
                    => new Text("", TypeRole.BodyM, context.Theme.TextPrimary);
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Tally.cs")
            .Single(result => result.ComponentName == "Tally").TypeScript;

        twin.Should().Contain("$hydration = { count: 'long' }");
    }

    /// <summary>A [Flags] enum is a NUMBER on this side, because the bits have to combine — so its
    /// default is 0 and not the name of whichever member happens to be zero.</summary>
    [Fact]
    public void AFlagsEnumDefaultsToZero_NotToItsZeroMembersName()
    {
        const string source = """
            using System;
            using eQuantic.UI.Primitives;

            [Flags]
            public enum Edges { None = 0, Top = 1, Bottom = 2 }

            [Component]
            public sealed class Framed : StatelessComponent
            {
                public Edges Sides { get; init; }
                public override VisualNode Build(ComponentContext context)
                    => new Text("", TypeRole.BodyM, context.Theme.TextPrimary);
            }
            """;
        var twin = new ComponentCompiler().CompileSource(source, "Framed.cs")
            .Single(result => result.ComponentName == "Framed").TypeScript;

        twin.Should().Contain("this.sides = 0").And.NotContain("this.sides = 'none'");
    }
}
