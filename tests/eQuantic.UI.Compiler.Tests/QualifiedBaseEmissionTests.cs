using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A base written with its namespace extends the twin's name, as the same base written bare does
/// (#479). The base was copied from its C# spelling, so <c>class Tag : eQuantic.UI.Web.HtmlElement</c>
/// emitted <c>extends eQuantic.UI.Web.HtmlElement</c>, a name nothing in the module defines, and the
/// module failed when it loaded. A component, a primitive, a record and a plain class each name their
/// base on their own path, and each is pinned here, with the project's model and without it.
/// </summary>
public class QualifiedBaseEmissionTests
{
    private const string Source = """
        using eQuantic.UI.Primitives;
        using UiBase = eQuantic.UI.Primitives.StatelessComponent;
        using Pail = App.Models.Bucket;

        namespace App.Models
        {
            public record Shape(int Sides);
            public class Bucket { public int Size { get; set; } }
        }

        namespace App
        {
            public sealed class Tag : eQuantic.UI.Web.HtmlElement
            {
                public string Href { get; set; } = "";
                public override eQuantic.UI.Web.HtmlNode Render() => new eQuantic.UI.Web.HtmlNode { Tag = "a" };
            }

            public sealed class Card : global::eQuantic.UI.Primitives.StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text("card", TypeRole.BodyM);
            }

            public sealed class Badge : UiBase
            {
                public override VisualNode Build(ComponentContext context) => new Text("badge", TypeRole.BodyM);
            }

            public record Square(int Side) : App.Models.Shape(4);

            public class Can : App.Models.Bucket { }

            public class Bin : Pail { }
        }
        """;

    [Theory]
    [InlineData("Tag", "HtmlElement")]
    [InlineData("Card", "StatelessComponent")]
    [InlineData("Badge", "StatelessComponent")]
    [InlineData("Square", "Shape")]
    [InlineData("Can", "Bucket")]
    [InlineData("Bin", "Bucket")]
    public void AQualifiedOrAliasedBase_ExtendsItsTwin_WithTheModel(string type, string twin)
    {
        var ts = Compile(withModel: true, type);

        ts.Should().MatchRegex($@"class {type} extends {twin}\b");
        ts.Should().NotContain("extends eQuantic").And.NotContain("extends global").And.NotContain("extends App.");
        // The name it extends is one the module brings in, from the runtime or from the twin's module.
        ts.Should().MatchRegex($@"import \{{[^}}]*\b{twin}\b[^}}]*\}}");
    }

    [Theory]
    [InlineData("Tag", "HtmlElement")]
    [InlineData("Card", "StatelessComponent")]
    [InlineData("Badge", "StatelessComponent")]
    [InlineData("Square", "Shape")]
    public void AQualifiedOrAliasedBase_ExtendsItsTwin_WithoutAModel(string type, string twin)
    {
        // A host with no project compilation reads the spelling: an alias through the directive the
        // file declares, and then the rightmost name, which is the twin's. A plain class is not here:
        // with no model it extends only a name the module knows it emits, which this one-file host
        // has not been told.
        var ts = Compile(withModel: false, type);

        ts.Should().MatchRegex($@"class {type} extends {twin}\b");
        ts.Should().NotContain("extends eQuantic").And.NotContain("extends global").And.NotContain("extends App.")
            .And.NotContain("extends UiBase").And.NotContain("extends Pail");
    }

    private static string Compile(bool withModel, string type)
    {
        var compiler = new ComponentCompiler();
        if (withModel)
        {
            var compilation = CSharpCompilation.Create("App",
                [CSharpSyntaxTree.ParseText(Source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, "App.cs")],
                References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
            compiler.SetProjectCompilation(compilation);
        }
        if (type is "Can" or "Bin")
        {
            // A plain class is emitted on the path an app build takes for one a component uses.
            var tree = CSharpSyntaxTree.ParseText(Source, eQuantic.UI.Compiler.Services.ParseDefaults.Options, "App.cs");
            var declaration = tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .Single(c => c.Identifier.Text == type);
            var model = withModel
                ? CSharpCompilation.Create("App", [tree], References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .GetSemanticModel(tree)
                : null;
            return new eQuantic.UI.Compiler.CodeGen.TypeScriptEmitter { SymbolsAreAuthoritative = withModel }
                .EmitPlainClassModule(declaration, model);
        }
        var result = compiler.CompileSource(Source, "App.cs").Single(r => r.ComponentName == type);
        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(TestReferences.Of)
            .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode)))
            .Append(TestReferences.Of(typeof(eQuantic.UI.Web.HtmlElement)));
}
