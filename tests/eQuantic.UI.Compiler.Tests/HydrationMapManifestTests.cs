using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A twin's hydration map lists what the hydration manifest says the server carries to the component,
/// under the names the twin declares. Built from the twin's own fields, the map never listed an
/// auto-property or a captured primary-constructor parameter, and the server named both after members
/// the twin does not have, so their values drew on the server and vanished on hydration.
/// </summary>
public class HydrationMapManifestTests
{
    private const string Source = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using eQuantic.UI.Primitives;

        namespace Shop;

        public abstract class LoadingPage : StatelessComponent
        {
            protected string _status = "";
        }

        [Page("/stats")]
        public sealed class Stats : LoadingPage, IServerPrefetch
        {
            public long Downloads { get; private set; }

            [ServerOnly]
            public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            {
                Downloads = 42;
                _status = "ready";
                return Task.CompletedTask;
            }

            public override VisualNode Build(ComponentContext context) => new Text($"{Downloads} {_status}", TypeRole.BodyM);
        }

        public sealed class Quote(string symbol) : StatelessComponent, IServerPrefetch
        {
            [ServerOnly]
            public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            {
                symbol = symbol.ToUpperInvariant();
                return Task.CompletedTask;
            }

            public override VisualNode Build(ComponentContext context) => new Text(symbol, TypeRole.BodyM);
        }

        public sealed class Badge(string text) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) => new Text(text, TypeRole.BodyM);
        }
        """;

    private static string Twin(string component)
    {
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(Source, "Shop.cs"));
        var result = compiler.CompileSource(Source, "Shop.cs").Single(r => r.ComponentName == component);
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void AnAutoProperty_AndAFieldABaseDeclares_AreListedUnderTheTwinsNames()
    {
        var twin = Twin("Stats");

        // The property by the name the twin reads it under, coerced by its type, and the base's field
        // beside it: the manifest describes the component with everything it inherits.
        Assert.Contains("return { _status: 'declared', downloads: 'long' };", twin);
        Assert.Contains("this.downloads", twin);
    }

    [Fact]
    public void ACapturedParameter_IsListed_SoTheTwinAdoptsItUnassigned()
    {
        // A twin assigns a captured parameter only when an argument arrived, so the runtime adopts it
        // because the map lists it, not because the instance holds it.
        var twin = Twin("Quote");

        Assert.Contains("return { symbol: 'declared' };", twin);
        Assert.Contains("this.symbol", twin);
    }

    [Fact]
    public void AComponentThatDoesNotPrefetch_ListsNothing()
    {
        // The server carries nothing to it, so there is nothing for its twin to adopt.
        Assert.DoesNotContain("$hydration", Twin("Badge"));
    }
}
