using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// What a number turns into when it becomes text, on both targets.
///
/// <para>
/// C#'s <c>ToString()</c> reads the culture the thread is in; JavaScript's <c>String(x)</c> is
/// always invariant. So the same expression renders "0,55" from a pt server and "0.55" in the
/// browser that hydrates over it, and nothing in either language says so. The author's escape is
/// <c>CultureInfo.InvariantCulture</c> — which the transpiler used to emit as a NAME, so the fix
/// for the divergence was itself a crash: "CultureInfo is not defined".
/// </para>
/// </summary>
public class CultureCrossingTests
{
    private static CompilationResult Compile(string body)
    {
        var source = $$"""
            using System;
            using System.Globalization;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Readout : StatelessComponent
            {
                private float _value = 0.55f;

                public override VisualNode Build(ComponentContext context) =>
                    new Text({{body}}, TypeRole.BodyM);
            }
            """;

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Primitives.VisualNode).Assembly.Location));

        var tree = CSharpSyntaxTree.ParseText(source, path: "Readout.cs");
        var compilation = CSharpCompilation.Create("Culture", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Readout.cs").Single();
    }

    /// <summary>The escape has to compile. `String(x)` IS .NET's invariant rendering of a
    /// number, so the ask is answered exactly rather than approximated.</summary>
    [Fact]
    public void TheInvariantCulture_CrossesAsPlainConversion()
    {
        var result = Compile("_value.ToString(CultureInfo.InvariantCulture)");

        Assert.True(result.Success);
        Assert.DoesNotContain("CultureInfo", result.TypeScript);
        Assert.Contains("String(this._value)", result.TypeScript);
    }

    /// <summary>With a specifier, the invariance has to reach the FORMATTER: every path in it reads
    /// the active culture, so a dropped provider is a number that follows whoever is reading the
    /// page — the opposite of what the author asked for.</summary>
    [Fact]
    public void TheInvariantCulture_WithASpecifier_ReachesTheFormatter()
    {
        var result = Compile("_value.ToString(\"0.##\", CultureInfo.InvariantCulture)");

        Assert.True(result.Success);
        Assert.DoesNotContain("CultureInfo", result.TypeScript);
        Assert.Contains("$eq.text.format(this._value, '0.##', undefined, true)", result.TypeScript);
    }

    /// <summary>A specifier alone is the CULTURE-following shape, and it already crossed correctly.
    /// It must keep crossing that way — no invariant flag, or every localized number in every app
    /// silently stops being localized.</summary>
    [Fact]
    public void ASpecifierAlone_StillFollowsTheAppsCulture()
    {
        var result = Compile("_value.ToString(\"N2\")");

        Assert.True(result.Success);
        Assert.Contains("$eq.text.format(this._value, 'N2')", result.TypeScript);
    }

    /// <summary>The quiet one: the shape everybody writes, which means two different things.</summary>
    [Fact]
    public void AFractionalNumber_WithNoCulture_IsFlagged()
    {
        var result = Compile("_value.ToString()");

        var warning = Assert.Single(result.Warnings, w => w.Code == "EQ2110");
        Assert.Contains("invariant", warning.Message);
        Assert.True(result.Success, "it compiles in apps today — the fix is one argument away");
    }

    /// <summary>
    /// The line that started this, whole: a rounded value converted for a machine. The receiver is
    /// a CALL rather than a field, which is the shape most likely to have been read as a format
    /// string, and it must come out as an ordinary conversion of the rounded number.
    /// </summary>
    [Fact]
    public void ARoundedValue_ConvertedInvariantly_CrossesWithNoCultureInIt()
    {
        var result = Compile("MathF.Round(_value, 2).ToString(CultureInfo.InvariantCulture)");

        Assert.True(result.Success);
        Assert.DoesNotContain("CultureInfo", result.TypeScript);
        Assert.Contains("String(", result.TypeScript);
    }

    /// <summary>A provider the subset cannot honour is refused where the developer can see it,
    /// which is the whole reason this file exists.</summary>
    [Fact]
    public void AProviderOutsideTheSubset_IsRefusedAtBuildTime()
    {
        var result = Compile("_value.ToString(CultureInfo.GetCultureInfo(\"de-DE\"))");

        Assert.Single(result.Errors, e => e.Code == "EQ2108");
        Assert.False(result.Success);
    }
}
