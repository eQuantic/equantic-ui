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
    // What the SDK puts in every file (Sdk.props): the factory surface and the write-once
    // component aliases. A page written against them names no namespace twice.
    private const string Page = """
        using static eQuantic.UI.Components.UI;
        using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;
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
            // Anchored on TYPES, never on AppDomain.GetAssemblies(): .NET loads assemblies
            // lazily, so a scan can miss the very assembly the code under test needs (the
            // factory surface) and every symbol comes back null — which looks exactly like a
            // compiler bug and is not one.
            var anchors = new[]
            {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(eQuantic.UI.Components.UI).Assembly,
                typeof(eQuantic.UI.Primitives.VisualNode).Assembly,
                typeof(eQuantic.UI.Primitives.PageAttribute).Assembly,
            };
            var references = anchors.Concat(AppDomain.CurrentDomain.GetAssemblies())
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Distinct()
                .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location));
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

    [Fact]
    public void WithReferences_ANamedArgumentLandsInItsOwnSlot()
    {
        // `Button("Count", onPressed: …)` skips variant and size. JS has only positions, so the
        // skipped ones must be filled from their defaults — emitted positionally, the handler
        // lands in the VARIANT slot and the button does nothing at all.
        Compile(withReferences: true).Should().Contain("UI.button('Count', 'primary', 'medium', () =>");
    }


    [Fact]
    public void TheRigidGap_IsReachableFromTheFactorySurface()
    {
        // A mirrored factory SHADOWS its own type inside any file that imports the surface, so
        // `Spacer.Fixed(34)` stops compiling there — the rigid spacer needs a name of its own or
        // it becomes unreachable in exactly the files the surface is meant for.
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var js = compiler.CompileSource("""
            using static eQuantic.UI.Components.UI;
            using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;
            using eQuantic.UI.Primitives;

            public sealed class HomePage : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    Column(gap: 0, children: [
                        Text("Title", TypeRole.Display),
                        Gap(34),
                        Text("Body", TypeRole.BodyM),
                    ]);
            }
            """, "HomePage.cs").Single().TypeScript;

        js.Should().Contain("UI.gap(34)").And.NotContain("this.gap(");
    }
}
