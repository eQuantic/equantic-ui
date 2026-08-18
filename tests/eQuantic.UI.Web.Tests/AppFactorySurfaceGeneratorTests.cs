using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The generator that gives an APP's own components the same declarative surface the SDK's have.
/// It is what turns a convention a developer must learn — write a factory class, keep it in step —
/// into a build that already knows.
/// </summary>
public class AppFactorySurfaceGeneratorTests
{
    private static (string Source, IReadOnlyList<Diagnostic> Diagnostics) Run(string appCode)
    {
        var tree = CSharpSyntaxTree.ParseText(appCode, path: "App.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Core.PageAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new AppFactorySurfaceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult().Results.Single();

        var generated = result.GeneratedSources.Length > 0
            ? result.GeneratedSources[0].SourceText.ToString()
            : "";
        return (generated, result.Diagnostics);
    }

    private const string Card = """
        using eQuantic.UI.Primitives;

        namespace Demo;

        public sealed class Card : StatelessComponent
        {
            public Card(string title) : this(title, null) { }
            public Card(string title, VisualNode? child, int weight = 3)
            {
                Title = title; Child = child; Weight = weight;
            }
            public string Title { get; init; }
            public VisualNode? Child { get; init; }
            public int Weight { get; init; }
            public override VisualNode Build(ComponentContext context) => new Text(Title);
        }
        """;

    [Fact]
    public void AComponent_GetsAFactoryNamedAfterIt_MirroringTheWidestConstructor()
    {
        var (source, diagnostics) = Run(Card);

        diagnostics.Should().BeEmpty();
        // The WIDEST — the same rule the emitter applies collapsing overloads, so the generated
        // factory and the transpiled constructor never disagree about what a Card is.
        source.Should().Contain("static global::Demo.Card Card(string title,");
        source.Should().Contain("int weight = 3", "a mirrored default keeps an omitted argument meaning the same");
        source.Should().NotContain("Card(string title)\n", "the narrow overload is not the one mirrored");
    }

    /// <summary>
    /// A capability never reaches the factory's signature: whoever composes a component in the
    /// middle of a tree has no container to reach into, and the framework already knows where to
    /// find one. What the factory passes says whether the component can cope without it — a
    /// nullable parameter gets whatever the scope has, a non-nullable one gets a sentence naming
    /// what is missing instead of a null travelling into the component.
    /// </summary>
    [Fact]
    public void ACapability_LeavesTheSignature_AndComesFromTheScope()
    {
        var (source, diagnostics) = Run("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Ticker(IClock clock, string label) : StatefulComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(label);
            }

            public sealed class Relaxed(IClock? clock, string label) : StatefulComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text(label);
            }
            """);

        diagnostics.Should().BeEmpty();
        source.Should().Contain("Ticker(string label)", "the caller composes it, and has no clock to give");
        source.Should().Contain("CapabilityScope.Require<global::eQuantic.UI.Primitives.IClock>(\"Ticker\")");
        source.Should().Contain("CapabilityScope.Resolve<global::eQuantic.UI.Primitives.IClock>()",
            "a component that declared it copes is left to cope");
    }

    [Fact]
    public void TheSurface_IsInScopeWithoutAnImport()
    {
        var (source, _) = Run(Card);

        source.Should().Contain("global using static Demo.AppUI;",
            "the factories reach every file the way the SDK's own surface does");
    }

    [Fact]
    public void APage_GetsNoFactory()
    {
        var (source, _) = Run("""
            using eQuantic.UI.Core;
            using eQuantic.UI.Primitives;

            namespace Demo;

            [Page("/home")]
            public sealed class HomePage : StatefulComponent
            {
                public override VisualNode Build(ComponentContext context) => new Text("x");
            }
            """);

        // A page is reached by its route, never composed by hand — offering a factory would be an
        // offer to do the wrong thing.
        source.Should().NotContain("HomePage(");
    }

    [Fact]
    public void AnElectedConstructor_WinsOverTheWidest()
    {
        var (source, diagnostics) = Run("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Badge : StatelessComponent
            {
                [UiFactory]
                public Badge(string label) { Label = label; }
                public Badge(string label, int count, bool dot) { Label = label; }
                public string Label { get; init; }
                public override VisualNode Build(ComponentContext context) => new Text(Label);
            }
            """);

        diagnostics.Should().BeEmpty();
        source.Should().Contain("Badge(string label)");
        source.Should().NotContain("bool dot", "the election beats the arity rule");
    }

    [Fact]
    public void TwoElectedConstructors_AreReportedRatherThanGuessed()
    {
        var (_, diagnostics) = Run("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public sealed class Badge : StatelessComponent
            {
                [UiFactory] public Badge(string label) { }
                [UiFactory] public Badge(int count) { }
                public override VisualNode Build(ComponentContext context) => new Text("x");
            }
            """);

        // An election with two winners has no answer, and picking one silently is how a component
        // ends up built the wrong way in a build nobody suspected.
        diagnostics.Should().ContainSingle(d => d.Id == "EQ3101" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void TwoComponentsSharingAName_AreReported_AndTheSurfaceStillExists()
    {
        var (source, diagnostics) = Run("""
            using eQuantic.UI.Primitives;

            namespace Demo.One
            {
                public sealed class Tile : StatelessComponent
                {
                    public Tile(string a) { }
                    public override VisualNode Build(ComponentContext context) => new Text("1");
                }
            }

            namespace Demo.Two
            {
                public sealed class Tile : StatelessComponent
                {
                    public Tile(string b) { }
                    public override VisualNode Build(ComponentContext context) => new Text("2");
                }

                public sealed class Panel : StatelessComponent
                {
                    public Panel(string c) { }
                    public override VisualNode Build(ComponentContext context) => new Text("3");
                }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "EQ3102" && d.Severity == DiagnosticSeverity.Warning);
        // The collision costs the loser its factory, never the rest of the surface.
        source.Should().Contain("Panel(");
        source.Should().Contain("Tile(");
        // One surface for the whole app, at the namespace the components share.
        source.Should().Contain("namespace Demo;");
    }

    [Fact]
    public void AnAbstractBase_IsNotAFactory()
    {
        var (source, _) = Run("""
            using eQuantic.UI.Primitives;

            namespace Demo;

            public abstract class CardBase : StatelessComponent
            {
                protected CardBase(string t) { }
            }

            public sealed class RealCard : CardBase
            {
                public RealCard(string t) : base(t) { }
                public override VisualNode Build(ComponentContext context) => new Text("x");
            }
            """);

        source.Should().NotContain("CardBase(");
        source.Should().Contain("RealCard(");
    }
}
