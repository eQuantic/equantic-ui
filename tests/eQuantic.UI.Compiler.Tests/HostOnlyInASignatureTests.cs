using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A host-only type named in a SIGNATURE, which is the seventh way to name a symbol and the only
/// one no expression strategy can reach.
///
/// <para>
/// `HostOnlyFrameworkTypeTests` covers the six that are expressions — a qualified call, a static
/// read, an unqualified call, a method group, a construction, an operator. A type POSITION is none
/// of those: `public Matrix2D Placement { get; init; }` names the type in the component's own
/// shape, where the only thing that sees it is the parser's semantic sweep. Measured before the
/// fix: it compiled, emitted `import { Matrix2D } from "@equantic/runtime"`, and would have taken
/// the page down at hydration on a name the runtime deliberately does not export. Found in review.
/// </para>
///
/// <para>
/// A whole compilation rather than a statement, because that is the condition: the defect lives in
/// the class's IMPORTS, and a statement probe has no class to import for.
/// </para>
/// </summary>
public class HostOnlyInASignatureTests
{
    private static CompilationResult Compile(string body) => CompileSource($$"""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Probe : StatelessComponent
            {
                {{body}}

                public override VisualNode Build(ComponentContext context) => Text("x", TypeRole.BodyM);
            }
            """);

    private static CompilationResult CompileSource(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(Components.Button).Assembly.Location));
        var compilation = CSharpCompilation.Create("SignatureProbe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetTypeByMetadataName("eQuantic.UI.Primitives.Matrix2D")
            .Should().NotBeNull("the probe has to compile against the real Primitives assembly");

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(source, "Probe.cs").Single();
    }

    [Theory]
    [InlineData("public Matrix2D Placement { get; init; }")]
    [InlineData("public RRect Corner { get; init; }")]
    [InlineData("private Matrix2D _placement;")]
    // The visitor's unit type: a component builds a tree, it does not visit one.
    [InlineData("public Nothing Marker { get; init; }")]
    // What a semantics WALK produces. No page constructs one — on the web the realizer writes ARIA
    // inline — and the day it stops, this row comes out together with the attribute.
    [InlineData("public SemanticNode Announced { get; init; }")]
    public void AHostOnlyTypeInAComponentsShape_IsStoppedAtCompileTime(string member)
    {
        var result = Compile(member);

        result.Success.Should().BeFalse("the runtime ships no export for it, so this would die at hydration");
        result.Errors.Should().Contain(error => error.Code == "EQ2010");
        result.TypeScript.Should().NotContain("Matrix2D",
            "and the name must not reach the import list either — an emitted import of a missing "
            + "export is the failure, whatever the build says about it");
    }

    /// <summary>
    /// `Accept` is HOST ONLY, and the fence follows an OVERRIDE to what it overrides.
    ///
    /// <para>
    /// A component BUILDS a tree; walking one is what a realizer or a layout pass does, and the
    /// runtime's own `VisualNode` has no `accept`. Measured before the fence: a page calling it
    /// compiled and emitted `node.accept(new Counter(), 0)`, which throws in the browser on a
    /// method that is not there — the quiet half of this family again, since the page renders on
    /// the server first.
    /// </para>
    ///
    /// <para>
    /// The receiver here is a `Text`, deliberately. A call binds to the symbol on the RECEIVER's
    /// type, so this resolves to `Text.Accept` and never sees the attribute on `VisualNode.Accept`
    /// — which is why the fence walks the override chain instead of the attribute being repeated on
    /// all 39 implementations, where the fortieth would forget.
    /// </para>
    /// </summary>
    [Fact]
    public void WalkingATree_IsNotSomethingAPageDoes()
    {
        var result = CompileSource("""
            using eQuantic.UI.Primitives;
            using static eQuantic.UI.Components.UI;

            namespace Demo;

            public sealed class Page : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    Text node = Text("x", TypeRole.BodyM);
                    IVisualNodeVisitor<int, int> visitor = null!;
                    var n = node.Accept(visitor, 0);
                    return Text($"{n}", TypeRole.BodyM);
                }
            }
            """);

        result.Success.Should().BeFalse("the runtime's VisualNode has no `accept`");
        result.Errors.Should().Contain(error => error.Code == "EQ2010");
    }

    /// <summary>
    /// The complement, and the reason the OPERATORS carry the attribute rather than `Point` itself:
    /// geometry a page can hold has twins and must keep crossing.
    /// </summary>
    [Theory]
    [InlineData("public Rect Box { get; init; }")]
    [InlineData("public Point Origin { get; init; }")]
    [InlineData("public Size Extent { get; init; }")]
    // The ENUMS beside that fenced record: they cross as string literals, so a component naming one
    // costs nothing — and fencing the record must not take them with it.
    [InlineData("public SemanticRole Role { get; init; }")]
    [InlineData("public SemanticCheck Checked { get; init; }")]
    public void TheGeometryAPageCanHold_StillReachesTheRuntime(string member)
    {
        var result = Compile(member);

        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        result.TypeScript.Should().Contain("@equantic/runtime");
    }
}
