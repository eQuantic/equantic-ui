using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Spec S1 authoring flows through eqc: a write-once component using group opacity, a fluent
/// <c>Transform2D</c>, aspect-ratio and align-self transpiles against the REAL Primitives assembly —
/// the static factory is class-qualified, the combinators camelCase, and the type rides the
/// runtime import like the rest of the vocabulary.
/// </summary>
public class S1StyleTranspilationTests
{
    private const string ComponentSource = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class RotatedBadge : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                var row = new Row(gap: Space.S2) { Cross = CrossAlign.End };
                row.Add(new Box(new BoxStyle
                {
                    Opacity = 0.85f,
                    Transform = Transform2D.Rotate(8f).WithScale(1.05f),
                    AspectRatio = 16f / 9f,
                    Width = SizeValue.Fill,
                }) { AlignSelf = CrossAlign.Start });
                return row;
            }
        }
        """;

    [Fact]
    public void S1Props_Transpile_WithQualifiedStaticsAndRuntimeImport()
    {
        var tree = CSharpSyntaxTree.ParseText(ComponentSource, path: "RotatedBadge.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("S1", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(ComponentSource, "RotatedBadge.cs").Single();

        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.TypeScript.Should().Contain("Transform2D.rotate(8).withScale(1.05)",
            "the static factory is class-qualified and the combinator camelCased");
        result.TypeScript.Should().Contain("opacity: 0.85");
        result.TypeScript.Should().Contain("alignSelf: 'start'");
        result.TypeScript.Should().MatchRegex("import \\{[^}]*Transform2D[^}]*\\} from \"@equantic/runtime\"",
            "the S1 value type rides the runtime import like the rest of the vocabulary");
    }
}
