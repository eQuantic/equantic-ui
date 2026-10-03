using eQuantic.UI.Generators;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The generator that writes the hydration manifest: what each component carries from its server
/// render to the browser, named by the member as C# declares it. The server and eqc both read these
/// entries, so they are pinned here as the source they are.
/// </summary>
public class HydrationManifestGeneratorTests
{
    private const string Attribute = "global::eQuantic.UI.Primitives.HydratedMember";
    private const string Kind = "global::eQuantic.UI.Primitives.HydratedMemberKind";

    private static (string Source, IReadOnlyList<Diagnostic> Errors) Run(string appCode)
    {
        var tree = CSharpSyntaxTree.ParseText(appCode, path: "App.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location));
        var compilation = CSharpCompilation.Create("App", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new HydrationManifestGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var result = driver.GetRunResult().Results.Single();

        var generated = result.GeneratedSources.Length > 0
            ? result.GeneratedSources[0].SourceText.ToString()
            : "";
        // The manifest is code the app compiles: a typeof the assembly cannot name, or a kind that
        // does not exist, would fail every build that has a prefetching component.
        var errors = updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        return (generated, errors);
    }

    private static string Entry(string component, string declaringType, string member, string kind) =>
        $"[assembly: {Attribute}(\"App.{component}\", \"App.{declaringType}\", \"{member}\", {Kind}.{kind})]";

    private const string Prices = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using eQuantic.UI.Primitives;

        namespace App;

        public abstract class LoadingPage : StatelessComponent
        {
            protected string _status = "";
        }

        public sealed class Prices(string symbol, string currency, IClock clock) : LoadingPage, IServerPrefetch
        {
            private decimal _price;
            public long Downloads { get; private set; }
            private readonly string _label = currency.ToUpperInvariant();
            public Action? OnRefresh { get; set; }
            private readonly IClock _clock = clock;

            [ServerOnly]
            public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            {
                _price = 1;
                Downloads = 42;
                symbol = symbol.Trim();
                return Task.CompletedTask;
            }

            public override VisualNode Build(ComponentContext context) => null!;
        }

        public sealed class Badge(string text) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => text.Length > 0 ? null! : null!;
        }
        """;

    [Fact]
    public void AField_AnAutoProperty_AndACapturedParameter_AreEachDescribedAsCSharpDeclaresThem()
    {
        var (source, errors) = Run(Prices);

        errors.Should().BeEmpty();
        source.Should().Contain(Entry("Prices", "Prices", "_price", "Field"))
            .And.Contain(Entry("Prices", "Prices", "Downloads", "Property"))
            .And.Contain(Entry("Prices", "Prices", "symbol", "CapturedParameter"));
    }

    [Fact]
    public void AParameterReadOnlyInAnInitializer_IsNotCaptured_AndItsFieldIsWhatCrosses()
    {
        // `currency` runs during construction, into `_label`; the C# compiler gives it no field of its
        // own, so there is nothing to describe for it but the field it fills.
        var (source, _) = Run(Prices);

        source.Should().Contain(Entry("Prices", "Prices", "_label", "Field"))
            .And.NotContain("\"currency\"");
    }

    [Fact]
    public void ADelegate_AndADependencyTheBrowserResolves_DoNotCross()
    {
        var (source, _) = Run(Prices);

        source.Should().NotContain("\"OnRefresh\"")
            .And.NotContain("\"_clock\"")
            .And.NotContain("\"clock\"");
    }

    [Fact]
    public void AnAppBase_IsDescribedAsTheTypeThatDeclaresTheMember()
    {
        var (source, _) = Run(Prices);

        source.Should().Contain(Entry("Prices", "LoadingPage", "_status", "Field"));
    }

    [Fact]
    public void AComponentThatDoesNotPrefetch_CarriesNothing()
    {
        var (source, _) = Run(Prices);

        source.Should().NotContain("\"App.Badge\"");
    }

    [Fact]
    public void APrivateNestedComponent_IsNamedAsTheRuntimeNamesIt()
    {
        // An assembly attribute cannot reach a private nested type with typeof, and a component may be
        // one, so a type is named by the name Type.FullName gives it.
        var (source, errors) = Run("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            namespace App;

            public sealed class Shell : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => null!;

                private sealed class Summary : StatelessComponent, IServerPrefetch
                {
                    private int _count;
                    public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                    { _count = 3; return Task.CompletedTask; }
                    public override VisualNode Build(ComponentContext context) => null!;
                }
            }
            """);

        errors.Should().BeEmpty();
        source.Should().Contain(Entry("Shell+Summary", "Shell+Summary", "_count", "Field"));
    }

    [Fact]
    public void AnythingThatPrefetches_IsDescribed_WhateverItDerivesFrom()
    {
        // An escape-hatch page prefetches without being a write-once component; its state crosses all
        // the same, so what decides is the prefetch, not the base.
        var (source, _) = Run("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            namespace App;

            public class Loader : IServerPrefetch
            {
                private string _loaded = "";
                public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                { _loaded = "x"; return Task.CompletedTask; }
            }
            """);

        source.Should().Contain(Entry("Loader", "Loader", "_loaded", "Field"));
    }
}
