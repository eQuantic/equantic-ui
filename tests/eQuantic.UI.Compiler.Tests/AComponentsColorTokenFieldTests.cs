using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A component's field of a vocabulary struct whose twin builds no zero, <c>ColorToken</c> here, is declared
/// as the runtime's <c>tsc</c> takes it (#1065): an instance field with no initializer definitely assigned, a
/// static one with no initializer, never <c>_ink: ColorToken = undefined;</c>, which TS2322 refused in a
/// library's component. The rule is #1142's, for every such struct; this pins the struct the issue found it on.
/// </summary>
public sealed class AComponentsColorTokenFieldTests
{
    [Fact]
    public void AColorTokenField_IsDeclaredAsTscTakesIt()
    {
        var source = """
            using eQuantic.UI.Primitives;
            public sealed class Inked : StatelessComponent
            {
                private readonly ColorToken _ink;
                private static ColorToken Shared;
                public Inked(ColorToken ink) { _ink = ink; Shared = ink; }
                public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM, _ink);
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "Inked.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("Inked", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the C# compiles");
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);

        var inked = compiler.CompileSource(source, "Inked.cs").Single().TypeScript;

        inked.Should().Contain("_ink!: ColorToken;", "the constructor assigns it, as C# definite assignment guarantees")
            .And.Contain("static shared: ColorToken;")
            .And.NotMatchRegex(@"ColorToken\s*=\s*undefined", "TypeScript refuses undefined under ColorToken");
    }
}
