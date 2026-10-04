using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A component that uses a <c>Color</c>, through the emitter an app build uses (#494): every member
/// lowers to the data the browser holds and to the companion of the type's name, and the module
/// imports that companion from the runtime, which is what makes the calls resolve when it loads.
/// The conformance suite executes the same members on both sides; this pins the module around them.
/// </summary>
public class TwinIsDataEmissionTests
{
    private const string Swatch = """
        using eQuantic.UI.Primitives;
        namespace App;
        public sealed class Swatch : StatelessComponent
        {
            private static readonly Color Brand = Color.FromRgb(0xF8, 0x71, 0x71);
            public override VisualNode Build(ComponentContext context)
            {
                var faded = Brand.WithOpacity(0.8f);
                var mid = Brand.MidpointWith(Color.White);
                Color made = new(1, 2, 3, 4);
                return new Text($"{faded} {mid.ToString()} {made.A} {default(Color).R}", TypeRole.BodyM);
            }
        }
        """;

    [Fact]
    public void AColorsMembers_LowerToItsCompanion_AndTheModuleImportsIt()
    {
        var ts = Compile(Swatch, "Swatch");

        ts.Should().MatchRegex(@"import \{[^}]*\bColor\b[^}]*\} from ""@equantic/runtime""");
        ts.Should().Contain("Color.withOpacity(Swatch.brand, Math.fround(0.8))");
        ts.Should().Contain("Color.midpointWith(Swatch.brand, Color.white)");
        ts.Should().Contain("$eq.text.record(faded, 'Color', ['R', 'G', 'B', 'A'])");
        ts.Should().Contain("$eq.text.record(mid, 'Color', ['R', 'G', 'B', 'A'])");
        ts.Should().Contain("{ r: 1, g: 2, b: 3, a: 4 }");
        ts.Should().Contain("{ r: 0, g: 0, b: 0, a: 0 }.r");
        ts.Should().NotContain(".withOpacity(Math").And.NotContain("String(mid)");
    }

    [Fact]
    public void AnAppsOwnColor_IsBuiltAsTheAppsType()
    {
        // The vocabulary's Color was recognized by its NAME, so this became `Color.fromRgb(...)`.
        var ts = Compile("""
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed record Color(string Name, int Hue, int Light);
            public sealed class Brand : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Text(new Color("brand", 10, 50).Name, TypeRole.BodyM);
            }
            """, "Brand");

        ts.Should().Contain("new Color('brand', 10, 50)").And.NotContain("fromRgb");
    }

    private static string Compile(string source, string component)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-twin-is-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, component + ".cs");
            File.WriteAllText(path, source);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)TestReferences.Of(p))
                .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .Append(TestReferences.Of(typeof(eQuantic.UI.Components.CodeEditor).Assembly.Location));
            var compilation = CSharpCompilation.Create("App", [CSharpSyntaxTree.ParseText(source, path: path)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var result = compiler.CompileFile(path).Single(r => r.ComponentName == component);
            result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result.TypeScript;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
