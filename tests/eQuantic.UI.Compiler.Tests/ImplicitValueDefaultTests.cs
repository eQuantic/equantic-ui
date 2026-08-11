using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A value-typed property with no initializer is ZERO in C#, and has to be zero on the client too.
/// <para>
/// It was <c>undefined</c>. Reads survived by luck for as long as they were tests —
/// <c>undefined > 0</c> is false, which is what 0 would have said — and then the first ARITHMETIC
/// turned it into NaN. A NaN width reaches the stylesheet as <c>width:NaNpx</c>, which the CSS
/// parser drops whole: the class is on the element and does nothing. It surfaced on a code block
/// after a client-side navigation and nowhere else, because SSR computes the same property in C#,
/// where it was 0 all along — the two targets quietly disagreed about the same field.
/// </para>
/// </summary>
public class ImplicitValueDefaultTests
{
    private static string TypeScriptOf(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(source, "Probe.cs").Single();
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    private const string Source = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Sized : StatelessComponent
        {
            public float Width { get; init; }
            public int Count { get; init; }
            public bool Wrapped { get; init; }
            public float? Optional { get; init; }
            public string? Label { get; init; }

            public override VisualNode Build(ComponentContext context) =>
                new Box(new BoxStyle { Width = SizeValue.Fixed(MathF.Max(120f, Width)) });
        }
        """;

    [Fact]
    public void ANumericProperty_DefaultsToZero_NotUndefined()
    {
        var typeScript = TypeScriptOf(Source);

        Assert.Contains("if (this.width === undefined) this.width = 0;", typeScript);
        Assert.Contains("if (this.count === undefined) this.count = 0;", typeScript);
    }

    [Fact]
    public void ABooleanProperty_DefaultsToFalse()
    {
        Assert.Contains("if (this.wrapped === undefined) this.wrapped = false;", TypeScriptOf(Source));
    }

    /// <summary>A NULLABLE value type really is null when unset — inventing a 0 there would be the
    /// same class of divergence pointing the other way.</summary>
    [Fact]
    public void ANullableOrReferenceProperty_IsLeftAlone()
    {
        var typeScript = TypeScriptOf(Source);

        Assert.DoesNotContain("this.optional = 0", typeScript);
        Assert.DoesNotContain("this.label =", typeScript);
    }
}
