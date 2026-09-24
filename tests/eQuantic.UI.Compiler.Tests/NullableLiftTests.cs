using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A compound, an increment or a unary operator on a NULLABLE number goes through the runtime's lift
/// with its type's rule inside (#372): JavaScript's own operator read a null as 0, so `x += 1` on a
/// null <c>int?</c> answered 1, and a <c>float?</c> or a <c>byte?</c> met none of their rules. The
/// behaviour is pinned on both sides by <c>NullableCompoundConformanceTests</c>; these pin the shape,
/// so a return to the native operator fails where no JavaScript engine runs.
/// </summary>
public class NullableLiftTests
{
    private static string Compile(string body)
    {
        var source = $$"""
            using eQuantic.UI.Primitives;

            public sealed class Probe : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
            {{body}}
                    return new Text("", TypeRole.BodyM);
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Lift", [tree], references,
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
    public void ACompoundOnANullableInt_IsLifted()
    {
        var js = Compile("int? x = null; x += 1;");
        Assert.Contains("x = $eq.nullable.arith(x, 1, (a, b) => a + b)", js);
    }

    [Fact]
    public void ACompoundOnANullableByte_WrapsInsideTheLift()
    {
        var js = Compile("byte? b = 250; b += 10;");
        Assert.Contains("$eq.nullable.arith(b, 10, (a, b) => (a + b & 0xFF))", js);
    }

    [Fact]
    public void ACompoundOnANullableDecimal_UsesTheDecimalInsideTheLift()
    {
        var js = Compile("decimal? m = null; m += 1m;");
        Assert.Contains("(a, b) => a.add(b))", js);
    }

    [Fact]
    public void AnIncrementOnANullableFloat_RoundsInsideTheLift()
    {
        var js = Compile("float? f = 0.1f; f++;");
        Assert.Contains("f = $eq.nullable.unary(f, (a) => Math.fround(a + 1))", js);
    }

    [Fact]
    public void ANegatedNullableInt_IsLifted()
    {
        var js = Compile("int? x = null; var y = -x;");
        Assert.Contains("$eq.nullable.unary(x, (a) => -a)", js);
    }
}
