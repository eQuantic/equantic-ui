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

    private const string ServerValues = """
        using eQuantic.UI.Primitives;

        namespace Shop;

        public sealed class SiteOptions
        {
            public string Title { get; set; } = "";
            public long MaxUpload { get; set; }
            public string ApiKey { get; set; } = "";
            public string Display => Title.ToUpperInvariant();
        }

        public sealed class SiteIdentity
        {
            public string Authority { get; set; } = "";
        }

        [Page("/about")]
        public sealed class AboutPage : StatelessComponent
        {
            private readonly SiteOptions _options;

            public AboutPage(SiteOptions options) { _options = options; }

            public override VisualNode Build(ComponentContext context) =>
                new Text($"{_options.Title} {_options.MaxUpload}", TypeRole.BodyM);
        }

        [Page("/login")]
        public sealed class LoginPage(SiteIdentity? identity) : StatelessComponent
        {
            public override VisualNode Build(ComponentContext context) =>
                new Text(identity is null ? "sign in" : "account", TypeRole.BodyM);
        }
        """;

    private static string ServerTwin(string component)
    {
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(ServerValues, "Server.cs"));
        var result = compiler.CompileSource(ServerValues, "Server.cs").Single(r => r.ComponentName == component);
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void AServerValue_IsAdoptedAsItsPlainProjection_WithTheLeavesItReadsCoerced()
    {
        // A plain object and never the class's twin: SiteOptions has one, whose getters would compute
        // from members that did not cross. The long it reads crosses as a string, so it is coerced.
        var twin = ServerTwin("AboutPage");

        Assert.Contains("return { _options: { members: { maxUpload: 'long' } } };", twin);
        Assert.DoesNotContain("_options: SiteOptions", twin);
    }

    [Fact]
    public void AServerValueReadOnlyForItsPresence_IsAnEmptyPlainProjection()
    {
        Assert.Contains("return { identity: { members: {} } };", ServerTwin("LoginPage"));
    }

    private static string GeneratedTwin(string source, string component)
    {
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(GeneratedProject.Of(source, "Generated.cs"));
        var result = compiler.CompileSource(source, "Generated.cs").Single(r => r.ComponentName == component);
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    [Fact]
    public void AMemberInheritedFromAGenericBase_IsCoercedAsTheComponentConstructsIt()
    {
        // The manifest names the base by its definition, LoadingPage`1, whose field is a List<T>: read
        // there, no spec says how to coerce it, and the longs the server writes as strings stay strings.
        var twin = GeneratedTwin("""
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            namespace Shop;

            public abstract class LoadingPage<T> : StatelessComponent
            {
                protected List<T> _items = new();
            }

            [Page("/counts")]
            public sealed class Counts : LoadingPage<long>, IServerPrefetch
            {
                [ServerOnly]
                public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                {
                    _items.Add(9007199254740993);
                    return Task.CompletedTask;
                }

                public override VisualNode Build(ComponentContext context) => new Text($"{_items.Count}", TypeRole.BodyM);
            }
            """, "Counts");

        Assert.Contains("return { _items: ['long'] };", twin);
    }

    [Fact]
    public void APropertyThatKeepsItsStoreThroughField_IsAdoptedIntoTheSlotTheTwinKeepsIt()
    {
        var twin = GeneratedTwin("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eQuantic.UI.Primitives;

            namespace Shop;

            [Page("/halves")]
            public sealed class Halves : StatelessComponent, IServerPrefetch
            {
                public long Half { get; set => field = value / 2; }

                [ServerOnly]
                public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
                {
                    Half = 84;
                    return Task.CompletedTask;
                }

                public override VisualNode Build(ComponentContext context) => new Text($"half {Half}", TypeRole.BodyM);
            }
            """, "Halves");

        // The slot the twin's own accessors read and write, coerced by the property's type.
        Assert.Contains("return { $half: 'long' };", twin);
        Assert.Contains("this.$half", twin);
    }
}
