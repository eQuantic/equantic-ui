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
/// itself (found in review, #359).
/// </summary>
public class CollectionComparerFenceTests
{
    [Theory]
    [InlineData("var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);")]
    [InlineData("Dictionary<string, int> d = new(StringComparer.OrdinalIgnoreCase) { [\"a\"] = 1 };")]
    [InlineData("var s = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);")]
    // A sorted dictionary is built by another strategy (a runtime map): the fence is not theirs.
    [InlineData("var m = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);")]
    public void AComparerThatChangesEqualityIsRefused(string statement)
    {
        ErrorsOf(statement).Should().Contain("EQ2007");
    }

    [Theory]
    [InlineData("var d = new Dictionary<string, int>(StringComparer.Ordinal);")]
    [InlineData("var d = new Dictionary<string, int>(EqualityComparer<string>.Default);")]
    [InlineData("var s = new SortedSet<string>(StringComparer.Ordinal);")]
    [InlineData("var s = new SortedSet<int>(Comparer<int>.Default);")]
    [InlineData("var l = new List<int>(16);")]
    [InlineData("var d = new Dictionary<string, int>(capacity: 4);")]
    public void AComparerThatAsksForWhatTheLoweringDoesPasses(string statement)
    {
        ErrorsOf(statement).Should().NotContain("EQ2007");
    }

    /// <summary>Compiled with the framework referenced, so the constructor BINDS: the fence reads the
    /// parameter a comparer lands in, which a standalone parse cannot see.</summary>
    private static IReadOnlyList<string> ErrorsOf(string statement)
    {
        var source = $$"""
            using System;
            using System.Collections.Generic;

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
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs")
            .SelectMany(result => result.Errors)
            .Select(error => error.Code)
            .ToList();
    }
}
