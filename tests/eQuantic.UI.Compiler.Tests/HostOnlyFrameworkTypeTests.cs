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
            using eQuantic.UI.Primitives;

            public class Probe
            {
                public void Method()
                {
                    {{statement}}
                }
            }
            """);

        var compilation = CSharpCompilation.Create("HostOnlyProbe", [tree],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
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

        reported.Code.Should().Be("EQ2004");
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
            .Should().Contain(d => d.Code == "EQ2004");
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
