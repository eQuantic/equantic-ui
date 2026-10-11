using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// SLICING. `text[a..b]` is the most ordinary thing a tokenizer, a formatter, or a parser writes,
/// and it used to transpile to <c>text[{ start: a, end: b }]</c> — an object handed to the index
/// operator, which is <c>undefined</c> every time, silently. It was found by writing a syntax
/// highlighter in C# and realizing it on the web, where every token came out blank.
/// </summary>
public class RangeIndexerTests
{
    [Theory]
    // The shapes JavaScript can say directly, at no runtime cost.
    [InlineData("text[1..4]", "text.slice(1, 4)")]
    [InlineData("text[start..end]", "text.slice(start, end)")]
    [InlineData("text[2..]", "text.slice(2)")]
    [InlineData("text[..4]", "text.slice(0, 4)")]
    [InlineData("text[..]", "text.slice(0)")]
    // `^` is what a negative argument to slice already means.
    [InlineData("text[..^1]", "text.slice(0, -1)")]
    [InlineData("text[^3..]", "text.slice(-3)")]
    [InlineData("text[1..^1]", "text.slice(1, -1)")]
    public void ARangeIndexIsASlice(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    /// <summary>
    /// The one case a negative index gets WRONG: `^0` is the end of the value, but `slice(0, -0)`
    /// is `slice(0, 0)` — empty. Anything that is not a positive literal after `^` could be zero,
    /// so it resolves against the length the way Index.GetOffset does.
    /// </summary>
    [Theory]
    [InlineData("text[..^0]", "$eq.slice(text, 0, false, 0, true)")]
    [InlineData("text[..^n]", "$eq.slice(text, 0, false, n, true)")]
    [InlineData("text[^n..]", "$eq.slice(text, n, true, null, false)")]
    public void AnEndpointThatMightBeZeroFromTheEnd_GoesThroughTheRuntime(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    [Fact]
    public void TheReceiverIsEvaluatedONCE_HoweverComplexItIs()
    {
        TestHelper.ConvertExpression("Line(index)[at..end]")
            .Should().Be("line(index).slice(at, end)",
                "a slice that called the method twice would do the work twice — and disagree with "
                + "itself when the method is not pure");
    }

    /// <summary>
    /// A range over an indexer that takes the <c>Range</c> itself hands it a System.Range value, which has
    /// no translation here: it is refused (EQ2004) at the range, where it was a call of a <c>slice</c>
    /// the twin does not have, or of a <c>Slice</c> that takes a length beside it (#585).
    /// </summary>
    [Theory]
    [InlineData("System.Range")]
    [InlineData("System.Range?")]   // reached by the range's implicit conversion, and refused the same way
    public void ARangeHandedToAnIndexerOverRange_IsRefused(string key)
    {
        var errors = ErrorsOf($$"""
            public class Ranged
            {
                public int Count => 5;
                public string this[{{key}} r] => "ranged";
                public int[] Slice(int start, int length) => new int[length];
            }

            public sealed class Probe
            {
                public object Run() => new Ranged()[1..3];
            }
            """);

        // The strategy's own refusal, and not merely its code: EQ2004 is every untranslated member's, so a
        // test of the code alone passes over any other refusal that reaches the same access.
        errors.Should().Contain(error => error.StartsWith("EQ2004: `1..3` is handed to Ranged's indexer over System.Range"));
    }

    /// <summary>Each error as its code and its message, compiled with the framework referenced, so the
    /// access BINDS: the bound tree names the member the range reaches, which a standalone parse cannot.</summary>
    private static IReadOnlyList<string> ErrorsOf(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)TestReferences.Of(p))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs")
            .SelectMany(result => result.Errors)
            .Select(error => $"{error.Code}: {error.Message}")
            .ToList();
    }
}
