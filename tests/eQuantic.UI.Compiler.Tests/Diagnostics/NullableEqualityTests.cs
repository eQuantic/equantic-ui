using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// Equals over a nullable struct, <c>Nullable.Equals</c> and <c>EqualityComparer&lt;T&gt;.Default.Equals</c>
/// build, where each was EQ2004 (#968), and lower to the comparison the default comparer of the value's
/// type is, which finds two empty values equal and an empty one unequal to any value. What the browser
/// answers with it is the conformance suite's (<c>NullableStructConformanceTests</c>). The comparer's
/// <c>Default</c> read on its own is still refused: no value the browser holds carries it, so only the call
/// made on it lowers. Compiled as an app's build compiles a file: one compilation behind the model.
/// </summary>
public class NullableEqualityTests
{
    private static string Source(string body) => $$"""
        using System;
        using System.Collections.Generic;

        public readonly record struct Spot(int Row, int Col);

        public static class Probe
        {
            public static object Same(Spot? a, Spot? b)
            {
                {{body}}
            }
        }
        """;

    [Theory]
    [InlineData("return a.Equals(b);")]
    [InlineData("return Nullable.Equals(a, b);")]
    [InlineData("return EqualityComparer<Spot?>.Default.Equals(a, b);")]
    public void ANullableStructsEquality_BuildsAsItsDefaultComparer(string body)
    {
        var probe = Compile(Source(body)).Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        probe.Errors.Concat(probe.Warnings).Select(diagnostic => $"{diagnostic.Code} {diagnostic.Message}")
            .Should().BeEmpty("the call lowers to the comparison its value type's default comparer is");
        probe.TypeScript.Should().Contain("$eq.equals(a, b)");
    }

    [Fact]
    public void TheDefaultComparerReadAlone_IsStillRefused()
    {
        var probe = Compile(Source("var comparer = EqualityComparer<Spot?>.Default; return comparer != null;"))
            .Should().ContainSingle(result => result.ComponentName == "Probe").Which;

        probe.Errors.Should().ContainSingle().Which.Message
            .Should().StartWith("'System.Collections.Generic.EqualityComparer<Spot?>.Default' has no JavaScript translation");
    }

    private static List<CompilationResult> Compile(string source)
    {
        var dir = Directory.CreateTempSubdirectory("eq-nullable-equality-").FullName;
        try
        {
            var path = Path.Combine(dir, "Probe.cs");
            File.WriteAllText(path, source);
            var tree = CSharpSyntaxTree.ParseText(source, ParseDefaults.Options, path: path);
            var compilation = CSharpCompilation.Create("App", [tree],
                ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                    .Split(Path.PathSeparator)
                    .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Select(p => TestReferences.Of(p)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

            var resolver = new ComponentDependencyResolver(compilation);
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            return compiler.CompileFile(path).ToList();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
