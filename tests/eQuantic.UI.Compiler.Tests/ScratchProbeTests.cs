using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

public class ScratchProbeTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        using static eQuantic.UI.Components.UI;

        namespace Demo;

        public sealed class Decl : StatefulComponent
        {
            private int _count;

            public override VisualNode Build(ComponentContext context) =>
                Column(gap: Space.S3, children: [
                    Text($"Count: {_count}", TypeRole.Display, context.Theme.TextPrimary),
                    Button("Up", onPressed: () => SetState(() => _count++)),
                ]);
        }
        """;

    [Fact]
    public void Probe_FactoryPage_WithRefs()
    {
        var tree = CSharpSyntaxTree.ParseText(Source, path: "Decl.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.UI).Assembly.Location));
        var compilation = CSharpCompilation.Create("Probe", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var csharpErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(Source, "Decl.cs").Single();

        File.WriteAllText("/private/tmp/claude-502/-Users-admin-edgar-a-mesquita-projects-equantic-equantic-ui/49769e12-f60f-466c-b793-ec1b263c3197/scratchpad/probe-factory.txt",
            "C# errors:\n" + string.Join("\n", csharpErrors)
            + "\n\neqc: " + (result.Success ? "SUCCESS" : "FAIL:\n" + string.Join("\n", result.Errors.Select(e => e.Message)))
            + "\n\n" + result.TypeScript);
        Assert.True(result.Success);
        Assert.Empty(csharpErrors);
    }
}
