using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Track L D2 (docs/I18N-PLAN.md): a resx accessor REWRITES to <c>$eq.str(id, key)</c> — never a
/// member read, and NEVER an inlined literal, because the accessor resolves against
/// <c>CurrentUICulture</c> at call time and a baked value is the build machine's language shipped
/// to production. The Designer shape here is the real ResXFileCodeGenerator output: an internal
/// NON-static class whose ResourceManager/Culture members are static.
/// </summary>
public class ResourceAccessorTests
{
    private const string DesignerSource = """
        namespace Demo.Resources
        {
            internal class Strings
            {
                private static global::System.Resources.ResourceManager? resourceMan;

                internal Strings() { }

                internal static global::System.Resources.ResourceManager ResourceManager =>
                    resourceMan ??= new global::System.Resources.ResourceManager(
                        "Demo.Resources.Strings", typeof(Strings).Assembly);

                internal static global::System.Globalization.CultureInfo? Culture { get; set; }

                internal static string Hero_Title
                {
                    get { return ResourceManager.GetString("Hero.Title", Culture)!; }
                }

                internal static string Greeting
                {
                    get { return ResourceManager.GetString("Greeting", Culture)!; }
                }
            }
        }
        """;

    private static CompilationResult Compile(string componentSource, params string[] extraSources)
    {
        var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(componentSource, path: "Page.cs"),
            CSharpSyntaxTree.ParseText(DesignerSource, path: "Resources/Strings.Designer.cs"),
        };
        for (var i = 0; i < extraSources.Length; i++)
            trees.Add(CSharpSyntaxTree.ParseText(extraSources[i], path: $"Extra{i}.cs"));

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("L", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        return compiler.CompileSource(componentSource, "Page.cs").Single();
    }

    /// <summary>The same compile, keeping the compiler — the catalog is aggregated ON it, across
    /// files, so a test about what ships has to look at the compiler and not at one result.</summary>
    private static ComponentCompiler CompileForCatalogue(string source)
    {
        var trees = new List<Microsoft.CodeAnalysis.SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(source, path: "Helper.cs"),
            CSharpSyntaxTree.ParseText(DesignerSource, path: "Resources/Strings.Designer.cs"),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("L3", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        foreach (var result in compiler.CompileSource(source, "Helper.cs"))
            Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return compiler;
    }

    private static IEnumerable<string> KeysFor(ComponentCompiler compiler, string id) =>
        compiler.ResourceUses.Where(r => r.Id == id).SelectMany(r => r.Keys);

    /// <summary>
    /// A key read from a STATIC HELPER reaches the catalog. It did not: the static-helper branch
    /// returns before the component path's tail, where the aggregation used to live, so the call
    /// site was rewritten to <c>$eq.str</c> and the key then left out of the catalog it looks up
    /// in. Server render correct, browser unable to switch language — the worst shape of bug,
    /// because every check that reads the served HTML passes.
    /// </summary>
    [Fact]
    public void KeyReadFromAStaticHelper_ReachesTheCatalogue()
    {
        var compiler = CompileForCatalogue("""
            using Demo.Resources;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public static class Copy
            {
                public static VisualNode Lead() => new Text(Strings.Hero_Title, TypeRole.BodyM);
            }
            """);

        Assert.Contains("Hero.Title", KeysFor(compiler, "Strings"));
    }

    /// <summary>A key read from a PLAIN CLASS reaches the catalog — same branch, same defect.</summary>
    [Fact]
    public void KeyReadFromAPlainClass_ReachesTheCatalogue()
    {
        var compiler = CompileForCatalogue("""
            using Demo.Resources;

            namespace Demo;

            public sealed class Menu
            {
                public string Label => Strings.Greeting;
            }
            """);

        Assert.Contains("Greeting", KeysFor(compiler, "Strings"));
    }

    /// <summary>And from a RECORD, whose module is emitted by a converter of its own — so it is
    /// not enough for the branches to reach the same method, they have to hand it their own
    /// converter's uses.</summary>
    [Fact]
    public void KeyReadFromARecord_ReachesTheCatalogue()
    {
        var compiler = CompileForCatalogue("""
            using Demo.Resources;

            namespace Demo;

            public sealed record Entry(string Id)
            {
                public string Title => Strings.Hero_Title;
            }
            """);

        Assert.Contains("Hero.Title", KeysFor(compiler, "Strings"));
    }

    private const string PageSource = """
        using Demo.Resources;
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Hero : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                new Text(Strings.Hero_Title, TypeRole.Display);
        }
        """;

    [Fact]
    public void Accessor_RewritesToARuntimeLookup_WithTheResxKey()
    {
        var result = Compile(PageSource);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // The KEY is what the getter hands to GetString ("Hero.Title"), not the mangled
        // property name (Hero_Title) — the catalog speaks resx, not C#.
        Assert.Contains("$eq.str(\"Strings\", \"Hero.Title\")", result.TypeScript);
        Assert.DoesNotContain("Strings.heroTitle", result.TypeScript);
    }

    [Fact]
    public void Accessor_IsNeverInlined_EvenThoughTheGetterShapeIsReadable()
    {
        // The evaluator CAN read `get { return …; }` bodies — the D2 pin is that it never does
        // for a resource class: no fragment of the lookup machinery lands as a literal.
        var result = Compile(PageSource);

        Assert.True(result.Success);
        Assert.DoesNotContain("GetString", result.TypeScript);
        Assert.DoesNotContain("ResourceManager", result.TypeScript);
    }

    [Fact]
    public void CompositeFormat_ComposesTheLookupIntoTheFormatHelper_AndNeverEvaluates()
    {
        var result = Compile("""
            using Demo.Resources;
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Welcome : StatelessComponent
            {
                public string UserName { get; init; } = "";

                public override VisualNode Build(ComponentContext context) =>
                    new Text(string.Format(Strings.Greeting, UserName), TypeRole.BodyM);
            }
            """);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        // D11: the template is per-culture DATA resolved at call time — the composite format
        // rides the runtime helper with the lookup inside, never a compile-time evaluation.
        Assert.Contains("$eq.text.stringFormat($eq.str(\"Strings\", \"Greeting\"), this.userName)",
            result.TypeScript);
    }

    [Fact]
    public void DesignerClass_IsNotAStaticHelperModule()
    {
        // A Designer swept into the transpile set must not become a Strings.ts full of
        // ResourceManager calls that cannot run in a browser.
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(DesignerSource, path: "Resources/Strings.Designer.cs"),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("L2", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var results = compiler.CompileSource(DesignerSource, "Resources/Strings.Designer.cs");

        Assert.DoesNotContain(results, r => r.ComponentName == "Strings");
    }

    [Fact]
    public void TwoResxSharingASimpleName_GetNamespaceQualifiedIds()
    {
        // The developer owns the resx and may name them anything — Admin.Strings and
        // Demo.Resources.Strings must not collide in one flat catalog.
        var adminDesigner = DesignerSource.Replace("namespace Demo.Resources", "namespace Admin");
        var result = Compile("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Mixed : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context)
                {
                    var column = new Column(gap: Space.S2);
                    column.Add(new Text(Demo.Resources.Strings.Hero_Title, TypeRole.Display));
                    column.Add(new Text(Admin.Strings.Hero_Title, TypeRole.Display));
                    return column;
                }
            }
            """, adminDesigner);

        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        Assert.Contains("$eq.str(\"Demo.Resources.Strings\", \"Hero.Title\")", result.TypeScript);
        Assert.Contains("$eq.str(\"Admin.Strings\", \"Hero.Title\")", result.TypeScript);
    }
}
