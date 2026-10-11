using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A comparer handed to a collection's constructor is fenced (EQ2007), never dropped. What these
/// collections lower to compares with <c>===</c> and ordinal strings, and the comparer used to
/// vanish: <c>CodeLanguages.For("CSharp")</c> answered C# natively and plain text in the browser,
/// and <c>new Dictionary&lt;string, int&gt;(comparer)</c> with no initializer became the comparer
/// itself (found in review, #359). A LINQ operator that finds its keys or its elements by a comparer
/// (<c>ToDictionary</c>, <c>ToLookup</c>, <c>GroupBy</c>, <c>Distinct</c>) passes the same fence (#578).
/// </summary>
public class CollectionComparerFenceTests
{
    [Theory]
    [InlineData("var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);")]
    [InlineData("Dictionary<string, int> d = new(StringComparer.OrdinalIgnoreCase) { [\"a\"] = 1 };")]
    [InlineData("var s = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);")]
    // A sorted dictionary is built by another strategy (a runtime map): the fence is not theirs.
    [InlineData("var m = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);")]
    // A LINQ operator that finds its keys or its elements by a comparer passes the same fence (#578):
    // ToDictionary refused every comparer under EQ1004, GroupBy under EQ2008, and Distinct and ToLookup
    // took this one without a word.
    [InlineData("var d = new[] { \"a\" }.ToDictionary(w => w, StringComparer.OrdinalIgnoreCase);")]
    [InlineData("var d = new[] { \"a\" }.ToDictionary(w => w, w => w.Length, StringComparer.OrdinalIgnoreCase);")]
    [InlineData("var l = new[] { \"a\" }.ToLookup(w => w, StringComparer.OrdinalIgnoreCase);")]
    [InlineData("var l = new[] { \"a\" }.ToLookup(w => w, w => w.Length, StringComparer.OrdinalIgnoreCase);")]
    [InlineData("var g = new[] { \"a\" }.GroupBy(w => w, StringComparer.OrdinalIgnoreCase);")]
    [InlineData("var x = new[] { \"a\" }.Distinct(StringComparer.OrdinalIgnoreCase);")]
    // Named and written before the key selector, where it is the comparer that is refused (see below).
    [InlineData("var g = new[] { \"a\" }.GroupBy(comparer: StringComparer.OrdinalIgnoreCase, keySelector: w => w);")]
    public void AComparerThatChangesEqualityIsRefused(string statement)
    {
        // The FENCE's refusal, and only it: a strategy that refuses the same construction again says the
        // same thing twice under a code that names no comparer (#577).
        ErrorsOf(statement).Should().Contain("EQ2007").And.NotContain("EQ1004");
    }

    [Theory]
    [InlineData("var d = new Dictionary<string, int>(StringComparer.Ordinal);")]
    [InlineData("var d = new Dictionary<string, int>(EqualityComparer<string>.Default);")]
    [InlineData("var s = new SortedSet<string>(StringComparer.Ordinal);")]
    [InlineData("var s = new SortedSet<int>(Comparer<int>.Default);")]
    [InlineData("var l = new List<int>(16);")]
    [InlineData("var d = new Dictionary<string, int>(capacity: 4);")]
    // The shape a site met on 0.2.0-preview.60 (#577): an ordinal dictionary with an indexer
    // initializer, target-typed, and its siblings, which #443 refused with EQ1004.
    [InlineData("Dictionary<string, int> d = new(StringComparer.Ordinal) { [\"a\"] = 1 };")]
    [InlineData("var d = new Dictionary<string, int>(new Dictionary<string, int>(), StringComparer.Ordinal);")]
    [InlineData("var d = new Dictionary<string, int>(4, StringComparer.Ordinal);")]
    [InlineData("var m = new SortedDictionary<string, int>(StringComparer.Ordinal);")]
    [InlineData("var m = new SortedList<string, int>(Comparer<string>.Default);")]
    // A LINQ operator's, which the lowering drops as it finds its keys that way already (#578).
    [InlineData("var d = new[] { \"a\" }.ToDictionary(w => w, StringComparer.Ordinal);")]
    [InlineData("var d = new[] { \"a\" }.ToDictionary(w => w, w => w.Length, EqualityComparer<string>.Default);")]
    [InlineData("var l = new[] { \"a\" }.ToLookup(w => w.Length, EqualityComparer<int>.Default);")]
    [InlineData("var g = new[] { \"a\" }.GroupBy(w => w, w => w.Length, (k, n) => k, StringComparer.Ordinal);")]
    [InlineData("var x = new[] { \"a\" }.Distinct((IEqualityComparer<string>)null);")]
    // GroupBy's comparer named and written first: the key selector was refused as a comparer.
    [InlineData("var g = new[] { \"a\" }.GroupBy(comparer: StringComparer.Ordinal, keySelector: w => w);")]
    public void AComparerThatAsksForWhatTheLoweringDoesPasses(string statement)
    {
        // No error at all, not merely no EQ2007: #443's refusal was EQ1004, which a check for the
        // fence's own code never saw.
        ErrorsOf(statement).Should().BeEmpty();
    }

    /// <summary>
    /// A comparer named and written before GroupBy's key selector is refused where it is written, and the
    /// key selector is not. The arguments after the first were matched to the parameters from the end, so
    /// the key selector was the one refused as a comparer, and the comparer was converted as the key
    /// selector (#578).
    /// </summary>
    [Fact]
    public void ANamedComparerWrittenFirst_IsRefusedWhereItIsWritten()
    {
        var refusals = ErrorsAt("var g = new[] { \"a\" }.GroupBy(comparer: StringComparer.OrdinalIgnoreCase, keySelector: w => w);");

        refusals.Should().ContainSingle().Which.Should().StartWith("EQ2007 at StringComparer.OrdinalIgnoreCase");
    }

    /// <summary>Compiled with the framework referenced, so the constructor BINDS: the fence reads the
    /// parameter a comparer lands in, which a standalone parse cannot see.</summary>
    private static IReadOnlyList<string> ErrorsOf(string statement) =>
        Compile(statement).Errors.Select(error => error.Code).ToList();

    /// <summary>Each error as its code and the source it is reported at, to the end of that line.</summary>
    private static IReadOnlyList<string> ErrorsAt(string statement)
    {
        var (source, errors) = Compile(statement);
        var lines = source.Split('\n');
        return errors.Select(error => $"{error.Code} at {lines[error.Line - 1][(error.Column - 1)..]}").ToList();
    }

    private static (string Source, IReadOnlyList<CompilationError> Errors) Compile(string statement)
    {
        var source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public sealed class Probe
            {
                public void Run()
                {
                    {{statement}}
                }
            }
            """;
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
        return (source, compiler.CompileSource(source, "Probe.cs").SelectMany(result => result.Errors).ToList());
    }
}
