using eQuantic.UI.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// EQ2100 and EQ2101 (docs/I18N-PLAN.md D7/D11): a resx template used through
/// <c>string.Format</c> is validated AT BUILD.
/// <para>
/// EQ2100 covers the call: the template must be a valid composite format whose specifiers the
/// browser reproduces EXACTLY (the M2 subset — `{0:N2}`, `{0:C}`, `{0:d}` and the culture-aware
/// plain `{0}` all pass now, over arguments of any type), and it must not ask for an argument the
/// call never passes. Alignment and specifiers outside the subset stay refused: a wrong number in
/// a foreign currency is worse than a compile error.
/// </para>
/// <para>
/// EQ2101 covers the TRANSLATIONS: every culture's template is held against the neutral one, so a
/// pt-BR string asking for {2} where the neutral has {0}/{1} fails the build instead of throwing
/// for Brazilian readers only.
/// </para>
/// </summary>
public class ResourceTemplateValidationTests
{
    private static CompilationResult CompileWithResx(string greetingTemplate, string callArgs,
        string argDeclarations = "public string UserName { get; init; } = \"\";",
        (string Culture, string Template)[]? translations = null)
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
        foreach (var (culture, translated) in translations ?? [])
            File.WriteAllText(Path.Combine(dir, $"Strings.{culture}.resx"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <root>
                  <data name="Greeting" xml:space="preserve">
                    <value>{System.Security.SecurityElement.Escape(translated)}</value>
                  </data>
                </root>
                """);
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
    public void ASubsetSpecifier_Passes()
    {
        // The M2 widening: these are the specifiers the cross-pinned fixture proves the browser
        // reproduces character for character.
        foreach (var template in new[] { "Total: {0:C}", "{0:N2} itens", "{0:P1}", "Em {0:d}", "{0:yyyy-MM-dd}" })
        {
            var result = CompileWithResx(template, "Amount", "public double Amount { get; init; }");
            Assert.True(result.Success,
                template + " → " + string.Join("\n", result.Errors.Select(e => e.Message)));
        }
    }

    [Fact]
    public void AlignmentIsStillRefused()
    {
        var result = CompileWithResx("Total: {0,10}", "UserName");
        AssertEq2100(result, "alignment");
    }

    [Fact]
    public void ASpecifierOutsideTheSubset_IsRefusedAtBuild()
    {
        // Hex of a negative value is two's complement at the C# TYPE's width, and the browser sees
        // one untyped number — outside the subset by construction.
        var result = CompileWithResx("Flags: {0:X4}", "Count", "public int Count { get; init; }");
        AssertEq2100(result, "outside the subset");
    }

    [Fact]
    public void ArityBeyondTheCall_IsRefusedAtBuild()
    {
        var result = CompileWithResx("{0} e {1}", "UserName");
        AssertEq2100(result, "expects argument {1}");
    }

    [Fact]
    public void ANonStringArgument_PassesNow()
    {
        // M0 refused this because a bare {0} formatted invariantly in the browser. The runtime now
        // formats through the culture exactly as .NET does, and the fixture pins it.
        var result = CompileWithResx("Você tem {0} itens", "Count", "public int Count { get; init; }");
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void ATranslationThatAsksForAnExtraArgument_FailsTheBuild()
    {
        var result = CompileWithResx("Hello, {0}!", "UserName",
            translations: [("pt-BR", "Olá, {0} e {1}!")]);

        var error = Assert.Single(result.Errors, e => e.Code == "EQ2101");
        Assert.Contains("pt-BR", error.Message);
        Assert.Contains("{0}, {1}", error.Message);
        Assert.False(result.Success);
    }

    [Fact]
    public void ATranslationThatDropsAPlaceholder_FailsTheBuild()
    {
        var result = CompileWithResx("Hello, {0}!", "UserName",
            translations: [("es", "¡Hola!")]);

        var error = Assert.Single(result.Errors, e => e.Code == "EQ2101");
        Assert.Contains("no placeholders", error.Message);
    }

    [Fact]
    public void AMalformedTranslation_FailsTheBuild()
    {
        var result = CompileWithResx("Hello, {0}!", "UserName",
            translations: [("pt-BR", "Olá, {0!")]);

        var error = Assert.Single(result.Errors, e => e.Code == "EQ2101");
        Assert.Contains("unterminated", error.Message);
    }

    [Fact]
    public void FaithfulTranslations_PassInEveryCulture()
    {
        var result = CompileWithResx("Hello, {0}!", "UserName",
            translations: [("pt-BR", "Olá, {0}!"), ("es", "¡Hola, {0}!"), ("de", "Hallo, {0}!")]);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
    }
}
