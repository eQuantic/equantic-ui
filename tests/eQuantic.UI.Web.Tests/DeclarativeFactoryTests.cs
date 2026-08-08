using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The DECLARATIVE factory surface (<c>using static eQuantic.UI.Components.UI</c>) is the official
/// authoring style — <c>Column(gap: …, children: [ … ])</c> instead of <c>new</c>. Every factory
/// call must translate through the class (<c>UI.column(…)</c>); resolved as a member of the page
/// (<c>this.column(…)</c>) it is undefined at runtime, which is a blank screen with no error.
/// </summary>
public class DeclarativeFactoryTests
{
    private const string Page = """
        using static eQuantic.UI.Components.UI;
        using eQuantic.UI.Core;
        using eQuantic.UI.Primitives;

        [Page("/")]
        public sealed class HomePage : StatefulComponent
        {
            private int _count;

            public override VisualNode Build(ComponentContext context) =>
                Column(gap: Space.S4, children: [
                    Text("MyApp", TypeRole.Display, context.Theme.TextPrimary),
                    Row(gap: Space.S3, children: [
                        Button("Count", onPressed: () => SetState(() => _count++)),
                        Button("Reset", Variant.Outline, onPressed: () => SetState(() => _count = 0)),
                    ]),
                ]);
        }
        """;

    private static string Compile(bool withReferences)
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        if (withReferences)
        {
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
            compiler.SetProjectCompilation(CSharpCompilation.Create("Probe",
                [CSharpSyntaxTree.ParseText(Page, path: "HomePage.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        }
        return compiler.CompileSource(Page, "HomePage.cs").Single().TypeScript;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryFactoryCall_GoesThroughTheClass(bool withReferences)
    {
        var js = Compile(withReferences);

        js.Should().Contain("UI.column(").And.Contain("UI.text(").And.Contain("UI.row(")
            .And.Contain("UI.button(");
        js.Should().NotContain("this.column(").And.NotContain("this.text(")
            .And.NotContain("this.row(").And.NotContain("this.button(");
        // …and the component model's own contract stays the component's: SetState reads exactly
        // like a factory call (Capitalized, unqualified, inherited), and routed through UI it
        // mounted a page whose buttons changed nothing.
        js.Should().Contain("this.setState(").And.NotContain("UI.setState(");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheFactoryClass_IsImported(bool withReferences)
    {
        Compile(withReferences).Should().MatchRegex(@"import \{[^}]*\bUI\b[^}]*\} from ""@equantic/runtime""");
    }
}
