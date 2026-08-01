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

    private const string MotionSource = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class GlidingCard : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context)
            {
                return new Box(new BoxStyle
                {
                    Transition = new TransitionSpec(StyleChannels.Opacity | StyleChannels.Transform, 300)
                    {
                        Easing = Curve.Decelerate,
                    },
                    Gradient = new LinearGradient(default, default) { ViaPosition = 0.25f },
                });
            }
        }
        """;

    /// <summary>
    /// A RUNTIME-provided value type constructed with an object initializer (`new T(a, b) { P = … }`)
    /// must fill the parameters the call site SKIPPED from their C# defaults before the trailing
    /// config object — otherwise the config lands in the next positional slot and the initialized
    /// member silently keeps its default (the S6 easing reverting to Standard).
    /// </summary>
    [Fact]
    public void RuntimeValueType_WithInitializer_FillsSkippedCtorParametersFirst()
    {
        var tree = CSharpSyntaxTree.ParseText(MotionSource, path: "GlidingCard.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("S6", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(MotionSource, "GlidingCard.cs").Single();

        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.TypeScript.Should().Contain("new TransitionSpec(2 | 4, 300, 0, { easing: Curve.decelerate })",
            "delayMs takes its default so the config never lands in a positional slot");
        result.TypeScript.Should().Contain("'toRight', { viaPosition: 0.25 }",
            "the gradient's direction defaults before the config object");
    }
}
