using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A constant outside the values a parameter allows is a build error (EQ3106, #1776): an <c>Icon</c> of
/// 22 dp built, rendered on the server and failed only where a browser built it, and the app guarded
/// it with a test that scanned its own sources. The values are the parameter's <c>[AllowedValues]</c>,
/// so the vocabulary's mark and an app's own are read alike.
/// </summary>
public class AllowedValueAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(Primitives.VisualNode).Assembly.Location))
            .Append(TestReferences.Of(typeof(Components.UI).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the probe is C# that compiles; only the analyzer may refuse it");
        return await compilation.WithAnalyzers([new AllowedValueAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }

    private static string Page(string body) => $$"""
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;

        public static class Probe
        {
            public static VisualNode Of(float runtimeSize) => {{body}};
        }
        """;

    [Theory]
    [InlineData("new Icon(Icons.Search, 22)", "22")]
    [InlineData("Icon(Icons.Search, 22)", "22")]
    [InlineData("Icon(Icons.Search, size: 18f)", "18")]
    [InlineData("new Icon(Icons.Search, 22.5f)", "22.5")]
    [InlineData("Icon(Icons.Search, (float?)22)", "22")]
    public async Task AnIconOfASizeOffTheScale_IsABuildError(string construction, string written)
    {
        var diagnostics = await AnalyzeAsync(Page(construction));

        var error = diagnostics.Should().ContainSingle().Which;
        error.Id.Should().Be("EQ3106");
        error.Severity.Should().Be(DiagnosticSeverity.Error);
        var message = error.GetMessage();
        message.Should().StartWith(written + " is not a value 'size' of ");
        message.Should().Contain("it takes null, 16, 20, 24 or 32",
            "the message names what the parameter takes, null for the size the box around the icon states included");
        message.Should().Contain("Vector", "and where a glyph of another size goes, from the mark's own words");
    }

    [Theory]
    [InlineData("new Icon(Icons.Search, 20)")]
    [InlineData("Icon(Icons.Search, 16)")]
    [InlineData("Icon(Icons.Search, (float)32d)")]
    [InlineData("Icon(Icons.Search, IconSize.Lg)")]
    [InlineData("Icon(Icons.Search)")]
    [InlineData("Icon(Icons.Search, size: null)")]
    [InlineData("new Icon(Icons.Search, runtimeSize)")]
    public async Task AnIconOnTheScale_OrOfASizeOnlyTheRunKnows_IsLeftAlone(string construction)
    {
        var diagnostics = await AnalyzeAsync(Page(construction));

        diagnostics.Should().BeEmpty("a step of the scale is a value the parameter allows, written as an int, a float, "
            + "a token or the default, and a size only known when the code runs is the constructor's to refuse");
    }

    [Fact]
    public async Task AnAppsOwnAllowedValues_AreReadTheSameWay()
    {
        var diagnostics = await AnalyzeAsync("""
            using System.ComponentModel.DataAnnotations;

            public static class Probe
            {
                public static string Mode([AllowedValues("grid", "list")] string mode) => mode;
                public static string Grid() => Mode("grid");
                public static string Table() => Mode("table");
            }
            """);

        var error = diagnostics.Should().ContainSingle().Which;
        error.Id.Should().Be("EQ3106");
        error.GetMessage().Should().Be("\"table\" is not a value 'mode' of Mode(…) allows: it takes \"grid\" or \"list\".");
    }

    [Fact]
    public async Task AllowedNumbers_CompareByTheirValue_WhateverTypeEachIsWrittenIn()
    {
        // The mark holds ints and the parameter is a double: the 2 a call hands arrives as 2d, which no
        // int equals, and the call is as right as the author meant it.
        var diagnostics = await AnalyzeAsync("""
            using System.ComponentModel.DataAnnotations;

            public sealed class Probe(double zoom)
            {
                public double Zoom { get; } = zoom;
                public static Probe Twice() => new Probe(2);
                public static Probe Thrice() => new Probe(3);
            }

            public sealed class Marked([AllowedValues(1, 2)] double zoom)
            {
                public double Zoom { get; } = zoom;
                public static Marked Twice() => new Marked(2);
                public static Marked Thrice() => new Marked(3.0);
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage())
            .Should().Equal("3 is not a value 'zoom' of new Marked(…) allows: it takes 1 or 2.");
    }

    [Fact]
    public async Task ANull_HandedToANullableParameterWhoseValuesLeaveItOut_IsABuildError()
    {
        // Unlike 22 on its way to a float?, a null literal converted to Nullable<int> is a constant C# folds itself,
        // written cast or not: a mark that leaves null out refuses it, and one that lists it takes it.
        var diagnostics = await AnalyzeAsync("""
            using System.ComponentModel.DataAnnotations;

            public static class Probe
            {
                public static int? Take([AllowedValues(1, 2)] int? count) => count;
                public static int? Maybe([AllowedValues(null, 1, 2)] int? count) => count;
                public static int? None() => Take(null);
                public static int? Cast() => Take((int?)null);
                public static int? Listed() => Maybe(null);
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Equal(
            "null is not a value 'count' of Take(…) allows: it takes 1 or 2.",
            "null is not a value 'count' of Take(…) allows: it takes 1 or 2.");
    }
}
