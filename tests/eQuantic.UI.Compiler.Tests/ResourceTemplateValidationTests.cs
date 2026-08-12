using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// EQ2100, the M0 slice (docs/I18N-PLAN.md D7/D11): a resx template used through
/// <c>string.Format</c> is validated AT BUILD against the neutral resx — plain positional
/// placeholders over string arguments pass; alignment, specifiers, arity mismatches and
/// non-string arguments are refused, never approximated at runtime.
/// </summary>
public class ResourceTemplateValidationTests
{
    private static CompilationResult CompileWithResx(string greetingTemplate, string callArgs,
        string argDeclarations = "public string UserName { get; init; } = \"\";")
    {
        var dir = Directory.CreateTempSubdirectory("eq-resx-").FullName;
        var designerPath = Path.Combine(dir, "Strings.Designer.cs");
        var resxPath = Path.Combine(dir, "Strings.resx");
        var pagePath = Path.Combine(dir, "Page.cs");

        var designerSource = """
            namespace Demo.Resources
            {
                internal class Strings
                {
                    internal Strings() { }

                    internal static global::System.Resources.ResourceManager ResourceManager =>
                        new global::System.Resources.ResourceManager(
                            "Demo.Resources.Strings", typeof(Strings).Assembly);

                    internal static global::System.Globalization.CultureInfo? Culture { get; set; }

                    internal static string Greeting
                    {
                        get { return ResourceManager.GetString("Greeting", Culture)!; }
                    }
                }
            }
            """;
        var resx = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="Greeting" xml:space="preserve">
                <value>{System.Security.SecurityElement.Escape(greetingTemplate)}</value>
              </data>
            </root>
            """;
        var pageSource = $$"""
            using Demo.Resources;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Welcome : StatelessComponent
            {
                {{argDeclarations}}

                public override VisualNode Build(ComponentContext context) =>
                    new Text(string.Format(Strings.Greeting, {{callArgs}}), TypeRole.BodyM);
            }
            """;

        File.WriteAllText(designerPath, designerSource);
        File.WriteAllText(resxPath, resx);
        File.WriteAllText(pagePath, pageSource);

        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(pageSource, path: pagePath),
            CSharpSyntaxTree.ParseText(designerSource, path: designerPath),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("V", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(pageSource, pagePath).Single();
    }

    private static void AssertEq2100(CompilationResult result, string fragment)
    {
        var error = Assert.Single(result.Errors, e => e.Code == "EQ2100");
        Assert.Contains(fragment, error.Message);
        Assert.False(result.Success);
    }

    [Fact]
    public void PlainPositional_OverAStringArgument_Passes()
    {
        var result = CompileWithResx("Olá, {0}!", "UserName");
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.DoesNotContain(result.Warnings, w => w.Code == "EQ2100");
    }

    [Fact]
    public void EscapedBraces_AreNotPlaceholders()
    {
        var result = CompileWithResx("{{literal}} e {0}", "UserName");
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void AFormatSpecifier_IsRefusedAtBuild()
    {
        var result = CompileWithResx("Total: {0:C}", "UserName");
        AssertEq2100(result, "alignment or a format specifier");
    }

    [Fact]
    public void ArityBeyondTheCall_IsRefusedAtBuild()
    {
        var result = CompileWithResx("{0} e {1}", "UserName");
        AssertEq2100(result, "expects argument {1}");
    }

    [Fact]
    public void ANonStringArgument_IsRefusedAtBuild()
    {
        var result = CompileWithResx("Você tem {0} itens", "Count",
            "public int Count { get; init; }");
        AssertEq2100(result, "formats per culture in .NET but not in the browser");
    }
}
