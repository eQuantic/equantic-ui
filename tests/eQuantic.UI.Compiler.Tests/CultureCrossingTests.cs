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
/// C#'s <c>ToString()</c> reads the culture the thread is in, and so does the browser's now: the
/// formatter writes it in the culture the server sent (#454, #471), where JavaScript's <c>String(x)</c>
/// rendered "0.55" over a pt server's "0,55". A value written for a machine says so with
/// <c>CultureInfo.InvariantCulture</c> — which the transpiler used to emit as a NAME, so the escape
/// was itself a crash: "CultureInfo is not defined".
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
            using Cultures = System.Globalization.CultureInfo;

            namespace Demo;

            // A member called InvariantCulture that is not CultureInfo's own: it may return any culture.
            public static class Lookalike
            {
                public static CultureInfo InvariantCulture => CultureInfo.CurrentCulture;
            }

            public sealed class Readout : StatelessComponent
            {
                private float _value = 0.55f;
                private string? _pattern;

                public override VisualNode Build(ComponentContext context) =>
                    new Text({{body}}, TypeRole.BodyM);
            }
            """;

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(Primitives.VisualNode).Assembly.Location));

        var tree = CSharpSyntaxTree.ParseText(source, path: "Readout.cs");
        var compilation = CSharpCompilation.Create("Culture", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Readout.cs").Single(result => result.ComponentName == "Readout");
    }

    /// <summary>The escape has to compile, and the invariant ask is answered exactly: by the
    /// printer that writes a float's own shortest digits, as .NET's invariant text does. `String()`
    /// was taken for that rendering and is not one: of this 0.55f it writes 0.550000011920929, the
    /// double underneath (#336).</summary>
    [Fact]
    public void TheInvariantCulture_CrossesAsPlainConversion()
    {
        var result = Compile("_value.ToString(CultureInfo.InvariantCulture)");

        Assert.True(result.Success);
        Assert.DoesNotContain("CultureInfo", result.TypeScript);
        Assert.Contains("$eq.num.single(this._value)", result.TypeScript);
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
        // The float says it is one (#378): its `G` and `R` digits are a single's.
        Assert.Contains("$eq.text.format(this._value, '0.##', undefined, true, 'single')", result.TypeScript);
    }

    /// <summary>A specifier alone is the CULTURE-following shape, and it already crossed correctly.
    /// It must keep crossing that way — no invariant flag, or every localized number in every app
    /// silently stops being localized.</summary>
    [Fact]
    public void ASpecifierAlone_StillFollowsTheAppsCulture()
    {
        var result = Compile("_value.ToString(\"N2\")");

        Assert.True(result.Success);
        Assert.Contains("$eq.text.format(this._value, 'N2', undefined, undefined, 'single')", result.TypeScript);
    }

    /// <summary>
    /// The shape everybody writes follows the culture on both targets (#454): a number's text with no
    /// specifier, and with the current culture or a null named, is the formatter's, in the culture the
    /// server sent. It was the invariant text under a warning that the targets disagreed (EQ2110),
    /// and the current culture named with no specifier was refused (EQ2109).
    /// </summary>
    [Theory]
    [InlineData("_value.ToString()")]
    [InlineData("_value.ToString(CultureInfo.CurrentCulture)")]
    [InlineData("_value.ToString((IFormatProvider)null)")]
    public void ANumberWithNoSpecifier_FollowsTheAppsCulture(string body)
    {
        var result = Compile(body);

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.DoesNotContain(result.Warnings, w => w.Code is "EQ2109" or "EQ2110");
        Assert.Contains("$eq.text.format(this._value, null, undefined, undefined, 'single')", result.TypeScript);
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
        Assert.Contains("$eq.num.single($eq.math.roundSingle(", result.TypeScript);
    }

    /// <summary>The culture is the PROPERTY the provider binds to, not its name: an alias names
    /// CultureInfo's own and crosses, and a lookalike of another type is refused, since it may
    /// return any culture. Reading a number follows the same rule, and so does the current culture:
    /// the thread's is the same value, but only CultureInfo's property is taken for it.</summary>
    [Theory]
    [InlineData("_value.ToString(Cultures.InvariantCulture)", true)]
    [InlineData("_value.ToString(Lookalike.InvariantCulture)", false)]
    [InlineData("decimal.Parse(\"1.5\", Cultures.InvariantCulture).ToString(CultureInfo.InvariantCulture)", true)]
    [InlineData("decimal.Parse(\"1.5\", Lookalike.InvariantCulture).ToString(CultureInfo.InvariantCulture)", false)]
    // The thread's culture is the same value, but not CultureInfo's property: it is named as that.
    [InlineData("_value.ToString(\"N2\", System.Threading.Thread.CurrentThread.CurrentCulture)", false)]
    [InlineData("_value.ToString(\"N2\", Cultures.CurrentCulture)", true)]
    public void ACulture_IsRecognisedByItsSymbol(string body, bool crosses)
    {
        var result = Compile(body);

        if (crosses)
        {
            Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
            Assert.DoesNotContain("CultureInfo", result.TypeScript);
            Assert.DoesNotContain("Cultures", result.TypeScript);
        }
        else
        {
            Assert.Single(result.Errors, e => e.Code == "EQ2108");
        }
    }

    /// <summary>A DateTime's ToString takes its provider as a number's does (#388): the invariant
    /// culture writes the invariant patterns, and a named one is refused at the build, where it
    /// crossed to the browser as a name no browser defines.</summary>
    [Fact]
    public void ADateTimesProvider_CrossesAsANumbersDoes()
    {
        var invariant = Compile("new DateTime(2026, 9, 24).ToString(\"D\", CultureInfo.InvariantCulture)");
        Assert.True(invariant.Success, string.Join("; ", invariant.Errors.Select(e => e.Message)));
        Assert.Contains("'D', undefined, true)", invariant.TypeScript);
        Assert.DoesNotContain("CultureInfo", invariant.TypeScript);

        var refused = Compile("new DateTime(2026, 9, 24).ToString(\"D\", CultureInfo.GetCultureInfo(\"de-DE\"))");
        Assert.Single(refused.Errors, e => e.Code == "EQ2108");
    }

    /// <summary>A date's ToString with no specifier is its type's own text in the current culture, as
    /// .NET's is — `G` of a DateTime, `d` of a DateOnly, `t` of a TimeOnly, a DateTimeOffset's `G` and
    /// offset — which the formatter knows, and so is one given the current culture, a null, or a null
    /// or an empty format, a variable's at run time; with the invariant culture it is the invariant
    /// text. It wrote the twin's invariant text (found in review, #472), and the other three types
    /// never reached the formatter (#469).</summary>
    [Theory]
    [InlineData("new DateTime(2026, 9, 24).ToString()", ", null)")]
    [InlineData("new DateTime(2026, 9, 24).ToString(CultureInfo.CurrentCulture)", ", null)")]
    [InlineData("new DateTime(2026, 9, 24).ToString((IFormatProvider)null)", ", null)")]
    [InlineData("new DateTime(2026, 9, 24).ToString(CultureInfo.InvariantCulture)", ", null, undefined, true)")]
    [InlineData("new DateTime(2026, 9, 24).ToString((string)null)", ", null)")]
    [InlineData("new DateTime(2026, 9, 24).ToString(\"\")", ", '')")]
    [InlineData("new DateTime(2026, 9, 24).ToString((string)null, CultureInfo.InvariantCulture)", ", null, undefined, true)")]
    [InlineData("new DateTime(2026, 9, 24).ToString(\"\", CultureInfo.CurrentCulture)", ", '')")]
    [InlineData("new DateTime(2026, 9, 24).ToString(_pattern)", ", this._pattern)")]
    [InlineData("new DateOnly(2026, 9, 24).ToString()", ", null)")]
    [InlineData("new DateOnly(2026, 9, 24).ToString(\"D\", CultureInfo.InvariantCulture)", ", 'D', undefined, true)")]
    [InlineData("new TimeOnly(10, 30).ToString(\"t\")", ", 't')")]
    [InlineData("new DateTimeOffset(2026, 9, 24, 10, 30, 0, TimeSpan.Zero).ToString(\"o\")", ", 'o')")]
    public void ADatesToString_WithNoSpecifier_IsItsTypesOwnText(string body, string emitted)
    {
        var result = Compile(body);
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        // The formatter's call, closing on what it is handed after the value.
        Assert.Matches(@"\$eq\.text\.format\(\$eq\.time\.\w+\([^;]*?\)" + System.Text.RegularExpressions.Regex.Escape(emitted),
            result.TypeScript);
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
