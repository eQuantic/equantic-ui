using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// <c>Sum()</c> over DECIMALS has to add them. A decimal crosses as a <c>$eq.num.dec</c> value, not
/// a JS number, so a reduction written with <c>+</c> concatenates instead: a payments total came out
/// as "R$ 01240.50640.00" — the seed, then each amount's text, glued end to end.
/// </summary>
public class DecimalSumTests
{
    private static string Compile(string body)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
            {{body}}
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Sum", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void SumOfDecimals_AddsThroughTheDecimalHelper()
    {
        var js = Compile("""
                    var values = new List<decimal> { 10.5m, 20.25m };
                    var total = values.Sum();
                    return new Text($"{total}", TypeRole.BodyM);
            """);

        // Whatever the reduction looks like, it must go through the decimal arithmetic — a bare
        // `a + b` over two `$eq.num.dec` values is string concatenation.
        Assert.DoesNotContain("(_a, _b) => _a + _b", js);
        Assert.Contains("$eq.num.dec(_a).add($eq.num.dec(_b))", js);
        Assert.Contains("$eq.num.dec(0))", js);
    }

    [Fact]
    public void SumWithASelector_AlsoAdds()
    {
        var js = Compile("""
                    var values = new List<decimal> { 10.5m, 20.25m };
                    var total = values.Sum(v => v);
                    return new Text($"{total}", TypeRole.BodyM);
            """);

        Assert.Contains(".add(", js);
    }

    [Fact]
    public void SumOfIntegers_StaysPlainArithmetic()
    {
        // The numeric path must not get slower or stranger for the common case.
        var js = Compile("""
                    var values = new List<int> { 1, 2 };
                    var total = values.Sum();
                    return new Text($"{total}", TypeRole.BodyM);
            """);

        Assert.Contains("reduce", js);
    }
}

/// <summary>
/// COMPOUND assignment on a decimal is the same arithmetic spelled shorter, and it went the same
/// wrong way: `total += amount` concatenated the two numbers' text. Found after switching an
/// example away from <c>Sum()</c> to a running total and watching it produce the same nonsense.
/// </summary>
public class DecimalCompoundAssignmentTests
{
    private static string Compile(string body)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
            {{body}}
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Compound", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void PlusEqualsOnDecimal_Adds()
    {
        var js = Compile("""
                    decimal total = 0m;
                    foreach (var v in new List<decimal> { 1.5m, 2.25m }) total += v;
                    return new Text($"{total}", TypeRole.BodyM);
            """);

        Assert.Contains(".add(", js);
        Assert.DoesNotContain("total += ", js);
    }

    [Fact]
    public void MinusEqualsOnDecimal_Subtracts()
    {
        var js = Compile("""
                    decimal total = 10m;
                    total -= 2.5m;
                    return new Text($"{total}", TypeRole.BodyM);
            """);

        Assert.Contains(".sub(", js);
    }

    [Fact]
    public void PlusEqualsOnANumber_StaysPlain()
    {
        var js = Compile("""
                    var count = 0;
                    count += 2;
                    return new Text($"{count}", TypeRole.BodyM);
            """);

        Assert.Contains("count += 2", js);
    }
}
