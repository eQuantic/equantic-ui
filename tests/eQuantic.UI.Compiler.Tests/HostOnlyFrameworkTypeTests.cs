using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A FRAMEWORK type marked <c>[ServerOnly]</c> must not be reachable from a component.
///
/// <para>
/// eqc routes the whole <c>eQuantic.UI.Primitives</c> namespace to <c>@equantic/runtime</c> without
/// an attribute on every type — that is what lets the shared vocabulary work. The cost is that the
/// routing is a NAMESPACE test, so it also routed the two types the runtime deliberately exports no
/// twin for. A component naming one compiled, emitted, and died at hydration on "does not provide
/// an export named", while SSR kept answering 200 with correct markup: everything looks right until
/// the screen is blank. <c>RouteValues</c> shipped exactly that way.
/// </para>
///
/// <para>
/// The attribute was INERT on a framework type until now — it declared host-only and nothing
/// enforced it. That is worse than saying nothing: a promise to catch something for you is one you
/// stop checking by hand. This is the test that makes the sentence true.
/// </para>
/// </summary>
public class HostOnlyFrameworkTypeTests
{
    /// <summary>
    /// A REAL compilation, referencing the real Primitives assembly. A minimal model is not enough
    /// and quietly answers the wrong question: with no reference the symbol does not bind, the
    /// converter takes its honest snippet-mode path, and the test passes while proving nothing. It
    /// did exactly that on the first attempt here.
    /// </summary>
    private static IReadOnlyList<ConversionDiagnostic> Diagnostics(string statement)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            using System;
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Primitives.FaceName;

            public class Probe
            {
                public void Method()
                {
                    {{statement}}
                }
            }
            """);

        // TPA carries this test host's project references, Primitives included (measured), so the
        // repo's other harnesses get away with TPA alone. Named EXPLICITLY here anyway: the whole
        // point of this suite is that it must not quietly go back to measuring snippet mode, and
        // that should not rest on how a host happens to compose its trusted list.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));

        var compilation = CSharpCompilation.Create("HostOnlyProbe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The reference has to actually be there, or this measures snippet mode again.
        compilation.GetTypeByMetadataName("eQuantic.UI.Primitives.FaceResolution")
            .Should().NotBeNull("the probe must compile against the real Primitives assembly");

        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        var body = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(method => method.Identifier.Text == "Method").Body!;
        converter.Convert(body.Statements.First());

        return converter.Diagnostics.ToList();
    }

    [Fact]
    public void AComponentTouchingTheFaceTally_IsStoppedAtCompileTime()
    {
        var reported = Diagnostics("FaceResolution.Missing(\"Ghost Sans\");")
            .Should().ContainSingle().Subject;

        reported.Code.Should().Be("EQ2010");
        reported.Message.Should().Contain("HOST ONLY", "the reader is told WHY, not just no");
        reported.Message.Should().Contain("hydration",
            "and where it would otherwise have failed, which is the part that costs an afternoon");
        reported.Message.Should().NotContain("add a strategy for it",
            "that remedy is for a type nobody decided about; this one was decided");
    }

    [Fact]
    public void TheBoundaryTally_IsFencedTheSameWay()
    {
        Diagnostics("ComponentBoundary.ClearContained();")
            .Should().Contain(d => d.Code == "EQ2010");
    }

    /// <summary>
    /// A READ, not a call — and the case this suite originally had and then lost. The first version
    /// of this test named <c>ComponentBoundary.Contained</c>, went red, and was changed to a method
    /// so it would pass: the test was fitted to the implementation instead of the implementation to
    /// the contract, and the property path stayed open. A fence that guards calls and not reads
    /// reads as protection while being none.
    /// </summary>
    [Theory]
    [InlineData("var seen = FaceResolution.Unresolved;")]
    [InlineData("var contained = ComponentBoundary.Contained;")]
    public void AStaticPropertyRead_IsNamingItJustAsMuch(string statement)
    {
        Diagnostics(statement).Should().Contain(d => d.Code == "EQ2010");
    }

    /// <summary>
    /// The MEMBER-level fence. <c>FaceName</c> itself crosses — the runtime exports it, and
    /// <c>IsWellFormed</c> is the rule both sides hold — while <c>Usable</c> writes the host's tally
    /// and does not. A type-only check accepts the containing type as provided and waves the member
    /// through, emitting a `faceName.usable` the runtime has never heard of.
    /// </summary>
    [Fact]
    public void AHostOnlyMEMBER_OnATypeThatCrosses_IsFencedByItself()
    {
        Diagnostics("var face = FaceName.Usable(\"IBM Plex Sans\");")
            .Should().Contain(d => d.Code == "EQ2010" && d.Message.Contains("HOST ONLY"));
    }

    [Fact]
    public void AndTheMemberBesideIt_StillCrosses()
    {
        Diagnostics("var ok = FaceName.IsWellFormed(\"IBM Plex Sans\");")
            .Should().BeEmpty("the rule is exactly what both sides are supposed to share");
    }

    /// <summary>
    /// The two branches that RETURN a name without going through the qualified path. Counting the
    /// ways a symbol can be named is the whole difficulty of this fence: each branch returns early
    /// on its own, so each has to be told, and a fence that guards three of four reads as protection
    /// for all four.
    /// </summary>
    [Theory]
    // `using static …FaceName;` — the call is unqualified and the declaring type never appears.
    [InlineData("var face = Usable(\"IBM Plex Sans\");")]
    // A method GROUP: named, not called, and emitted exactly the same way.
    [InlineData("Func<string, string> f = Usable;")]
    public void EveryWayOfNamingIt_IsTheSameNaming(string statement)
    {
        Diagnostics(statement).Should().Contain(d => d.Code == "EQ2010");
    }

    [Fact]
    public void AnUnqualifiedCallToTheMemberThatCrosses_IsStillFine()
    {
        Diagnostics("var ok = IsWellFormed(\"IBM Plex Sans\");")
            .Should().BeEmpty("the fence is per symbol, not per import");
    }

    /// <summary>
    /// CONSTRUCTION, which the fence did not count. `Matrix2D.Identity` — a static read — was
    /// stopped, and `new Matrix2D(...)` two lines above it was not: it compiled, emitted an import
    /// of a name the runtime ships no export for, and took the page down at hydration. Measured on
    /// a real page before this branch existed.
    /// </summary>
    [Theory]
    [InlineData("var m = new Matrix2D(1, 0, 0, 1, 0, 0);")]
    [InlineData("var r = new RRect(new Rect(0, 0, 4, 4));")]
    // TARGET-TYPED, which is the same naming spelled shorter — and the shape the first version of
    // this fence missed, having guarded only the explicit one. Found in review.
    [InlineData("Matrix2D m = new(1, 0, 0, 1, 0, 0);")]
    [InlineData("RRect r = new(new Rect(0, 0, 4, 4));")]
    public void ConstructingAHostOnlyType_IsNamingItToo(string statement)
    {
        Diagnostics(statement).Should().Contain(d => d.Code == "EQ2010" && d.Message.Contains("HOST ONLY"));
    }

    /// <summary>
    /// An OPERATOR, which the fence did not count either — and this one fails SILENTLY rather than
    /// loudly, which makes it the worse half. JavaScript cannot overload an operator, so `a + b` on
    /// two framework values emits JavaScript's own `+`: two `Point` objects concatenate into
    /// `"[object Object][object Object]"` and `a * 2` is `NaN`, in a page that compiled, rendered
    /// on the server and looked fine.
    ///
    /// <para>
    /// Measured, not reasoned: a page doing exactly this emitted `let sum = a + b;` and
    /// `let scaled = a * 2;`. Raised by Copilot on #135.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("var sum = new Point(1, 2) + new Point(3, 4);")]
    [InlineData("var diff = new Point(1, 2) - new Point(3, 4);")]
    [InlineData("var scaled = new Point(1, 2) * 2f;")]
    public void AFrameworkOperatorWithNoTwin_IsStoppedRatherThanEmitted(string statement)
    {
        Diagnostics(statement).Should().Contain(d => d.Code == "EQ2010" && d.Message.Contains("HOST ONLY"));
    }

    /// <summary>
    /// …and the type itself still crosses, which is the whole point of fencing the OPERATORS rather
    /// than the type. A page can hold a box and read its edges; it just cannot do arithmetic
    /// JavaScript would answer wrongly.
    /// </summary>
    [Theory]
    [InlineData("var p = new Point(1, 2);")]
    [InlineData("var box = new Rect(0, 0, 10, 10);")]
    [InlineData("var edge = new Rect(0, 0, 10, 10).Right;")]
    [InlineData("var hit = new Rect(0, 0, 10, 10).Contains(new Point(1, 1));")]
    [InlineData("Rect box = new(0, 0, 10, 10);")]
    [InlineData("Point p = new(1, 2);")]
    public void TheGeometryAPageCanHold_StillCrosses(string statement)
    {
        Diagnostics(statement).Should().BeEmpty(
            "Point, Size and Rect have twins — fencing the type would have taken those with it");
    }

    /// <summary>
    /// The control, and the one that matters more than the two above: the namespace is still routed.
    /// A fence that also blocked the ordinary vocabulary would be caught by every other test in the
    /// suite, but as a hundred confusing failures rather than as one clear one.
    /// </summary>
    [Fact]
    public void TheRestOfThePrimitivesNamespace_StillCrosses()
    {
        Diagnostics("var insets = EdgeInsets.All(8f);")
            .Should().BeEmpty("the shared vocabulary is exactly what the runtime does ship");
    }
}
