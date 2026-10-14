using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The harness checked against itself. A differential suite is only worth its runtime if a
/// program it CANNOT run fails loudly — a silent skip turns every unsupported construct into a
/// green tick, which is the one outcome worse than a red one.
/// </summary>
public class HarnessSelfChecksTests
{
    [SkippableFact]
    public void AProgramThatDoesNotCompile_FailsRatherThanPasses()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");

        // Not valid C# in any language version, so neither side can produce a value.
        Assert.ThrowsAny<Exception>(() =>
            ConformanceRunner.AssertStatementsSameAsDotNet("var x = ; return $\"{x}\";", ""));
    }

    /// <summary>
    /// A case whose C# the build refuses fails before anything runs, naming the refusal (#907). The
    /// converter still writes text where it refuses, the query's source alone here, and the runner ran
    /// it: this case answered 3 where .NET answers 6, and read as a translation bug (#761).
    /// </summary>
    [Fact]
    public void ACaseTheBuildRefuses_FailsNamingTheRefusal_AndRunsNothing()
    {
        var refused = Assert.Throws<RefusedByTheBuildException>(() =>
            ConformanceRunner.AssertStatementsSameAsDotNet("return (from a in new[] { 1, 2 } let f = a * 2 select f).Sum();"));

        refused.Refusals.Select(refusal => refusal.Id).Should().Equal("EQ2008");
        refused.Message.Should().Contain("EQ2008: query syntax with a 'let' clause is not lowered");
    }

    /// <summary>
    /// What refuses is the build's error, never its warning: a number read with no culture is warned
    /// about (EQ2110) and built, so the case runs.
    /// </summary>
    [Fact]
    public void AWarning_RefusesNothing()
    {
        Refusal.Of([
                new ConversionDiagnostic(ConversionSeverity.Warning, "EQ2110", "a warning", 1, 1),
                new ConversionDiagnostic(ConversionSeverity.Error, "EQ2008", "an error", 1, 1),
            ])
            .Select(refusal => refusal.Id).Should().Equal("EQ2008");
        Transpiler.TranslateStatements("return double.Parse(\"2.5\", null);").Refusals.Should().BeEmpty();
    }

    /// <summary>
    /// What a build runs BESIDE the converter is read too, over the prelude's declarations: a record
    /// with two methods of one name is refused (EQ1007), where the harness alone would have written
    /// both and JavaScript kept the second.
    /// </summary>
    [Fact]
    public void APreludeTheBuildRefuses_FailsTheCase_NamingTheModule()
    {
        var refused = Assert.Throws<RefusedByTheBuildException>(() => Transpiler.TranspileStatements(
            "return new Twice(1).Plus(2);", Twice));

        refused.Refusals.Should().ContainSingle(refusal => refusal.Id == "EQ1007" && refusal.Module == "Twice");
    }

    /// <summary>A record the build refuses, two methods of one name (EQ1007).</summary>
    private const string Twice =
        "public record Twice(int X) { public int Plus(int y) => X + y; public int Plus(int y, int z) => X + y + z; }";

    /// <summary>A record the build refuses with a query's fence (EQ2008), in a method of its own.</summary>
    private const string Box =
        "public record Box(int X) { public int Doubled() => (from a in new[] { X } let f = a * 2 select f).Sum(); }";

    private const string LetQuery = "return (from a in new[] { 1, 2 } let f = a * 2 select f).Sum();";

    /// <summary>
    /// A case written for a fence pins THAT fence, in its OWN C#: it fails when the build accepts the
    /// case's C#, its prelude's refusal not standing in for it, when the build refuses it for another
    /// reason, its prelude's included, and when it is not C# the compiler accepts.
    /// </summary>
    [Theory]
    [InlineData("return 1;", "EQ2008", "", "the build accepts the case's own C#")]
    [InlineData(LetQuery, "EQ2004", "", "EQ2008")]
    [InlineData("return (from a in nothing let f = a * 2 select f).Sum();", "EQ2008", "", "C# that compiles")]
    // The prelude alone is refused with the id: the case's own C# is not, so the case does not pin it.
    [InlineData("return new Twice(1).Plus(2);", "EQ1007", Twice, "the build accepts the case's own C#")]
    // The case's own C# is refused with the id, and its prelude for another reason.
    [InlineData(LetQuery, "EQ2008", Twice, "EQ1007 in Twice")]
    public void AnExpectedRefusal_FailsWhereTheBuildSaysOtherwise(string statements, string id, string prelude, string named)
    {
        var failure = Assert.ThrowsAny<Exception>(() => ConformanceRunner.AssertStatementsRefusedByTheBuild(statements, id, prelude));
        failure.Should().NotBeOfType<RefusedByTheBuildException>();
        failure.Message.Should().Contain(named);
    }

    /// <summary>
    /// A prelude refused with the SAME id as the case's own C# stands beside it: the case pins its own
    /// refusal, and nothing in the prelude is another reason.
    /// </summary>
    [Fact]
    public void APreludeRefusedWithTheSameId_StandsBesideTheCasesOwn()
    {
        Transpiler.TranslateStatements("return 0;", Box).PreludeRefusals
            .Should().Contain(refusal => refusal.Id == "EQ2008" && refusal.Module == "Box");
        ConformanceRunner.AssertStatementsRefusedByTheBuild(LetQuery, "EQ2008", Box);
    }

    /// <summary>
    /// A source compiled as an app's build compiles it is asked about EVERY module before the modules
    /// that wrote text are kept: a class with two methods of one name stops the build before its
    /// emitter runs (EQ1007), so it writes none, and a case that never names it passed while it was
    /// dropped with its refusal.
    /// </summary>
    [SkippableFact]
    public void AModuleTheBuildRefusesBeforeItWritesText_FailsTheSource()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var failure = Assert.ThrowsAny<Exception>(() => ModuleGraph.AssertSameAsDotNet(
            "public class Twice { public int Plus(int y) => y; public int Plus(int y, int z) => y + z; }",
            typeAnnotations: false,
            ("Untouched", "return 1;")));
        failure.Message.Should().Contain("EQ1007 in Twice");
    }

    /// <summary>
    /// The bundle every helper-using case imports LOADS. When it did not — a hydration map named a
    /// class, as a static field initializer, inside an import cycle that had not defined it yet — a
    /// few hundred cases failed at once, each naming its own expression and none naming the one
    /// ReferenceError they shared.
    /// </summary>
    [SkippableFact]
    public void TheRuntimeBundle_Loads()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Assert.Equal("object", ConformanceRunner.ImportTheRuntimeBundle());
    }

    /// <summary>
    /// A representation the translation got wrong FAILS (#596). The .NET side writes a value as the
    /// runtime holds it, so a long that became a JS number, a decimal that became one, an enum written
    /// as its number, a float left a double and a tuple written as an object each print what .NET's
    /// value does not, where System.Text.Json's own JSON matched the first four. The right
    /// representation, printed the same way, matches.
    /// </summary>
    [SkippableTheory]
    [InlineData("return 5L;", "5", "5n")]
    [InlineData("return 1.5m;", "1.5", null)]
    [InlineData("return Day.Friday;", "1", "'friday'")]
    [InlineData("return 0.1f;", "0.1", "Math.fround(0.1)")]
    [InlineData("return (1, 2L);", "({ item1: 1, item2: 2n })", "[1, 2n]")]
    public void AWrongRepresentation_Fails(string statements, string wrong, string? right)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        var expected = DotNetEvaluator.EvaluateToJson(statements, "public enum Day { Monday, Friday }");
        Assert.NotEqual(expected, JsExecutor.Run(ConformanceRunner.Print(wrong)));
        if (right is not null) Assert.Equal(expected, JsExecutor.Run(ConformanceRunner.Print(right)));
    }

    /// <summary>
    /// Both sides parse with <c>LanguageVersion.Preview</c>, the same as eqc. On Roslyn's default
    /// — the latest RELEASED version — a construct eqc accepts would fail to parse in the harness
    /// and read as a translation bug rather than as a harness that lags.
    /// </summary>
    [SkippableTheory]
    [InlineData("var a = -3; var b = a >>> 1; return $\"{b}\";")]              // C# 11
    [InlineData("var xs = new[] { 1, 2, 3 }; return $\"{xs[^1]}{xs[1..].Length}\";")]  // C# 8
    [InlineData("int Twice(int v) => v * 2; return $\"{Twice(21)}\";")]        // local function
    public void TheHarnessAcceptsWhatEqcAccepts(string program)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(program, "");
    }
}
