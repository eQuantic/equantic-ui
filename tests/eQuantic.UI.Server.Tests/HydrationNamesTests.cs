using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The names a component's state crosses under, read off the document the server sends.
/// <para>
/// The twin adopts a value only into a member it declares under that exact name, and eqc names a twin's
/// members by one rule. The server named them after the fields the C# compiler synthesized instead, so an
/// auto-property crossed as <c>Downloads</c> into a twin declaring <c>downloads</c>, a public field
/// spelled in Pascal case did the same, and a captured primary-constructor parameter crossed as
/// <c>&lt;symbol&gt;P</c>. Each value drew on the server and vanished on hydration. These read what the
/// payload now carries, from the manifest the generator wrote into this assembly.
/// </para>
/// </summary>
public class HydrationNamesTests
{
    [Page("/hydration-names")]
    public sealed class NamesPage : StatelessComponent, IServerPrefetch
    {
        public int Downloads;
        public int Visits { get; private set; }
        private string _status = "";

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Downloads = 7;
            Visits = 42;
            _status = "ready";
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context)
        {
            var page = new Column();
            page.Add(new Text($"{Downloads} {Visits} {_status}", TypeRole.BodyM));
            page.Add(new Quote("acme"));
            return page;
        }
    }

    /// <summary>A child whose prefetch rewrites the primary-constructor parameter it captured.</summary>
    public sealed class Quote(string symbol) : StatelessComponent, IServerPrefetch
    {
        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            symbol = symbol.ToUpperInvariant();
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text(symbol, TypeRole.BodyM);
    }

    [Page("/hydration-nested")]
    public sealed class NestedComponentPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Hidden();

        /// <summary>
        /// A prefetching component no assembly attribute can reach through typeof: the manifest names it
        /// by the name the runtime gives it, so its state crosses like any other.
        /// </summary>
        private sealed class Hidden : StatelessComponent, IServerPrefetch
        {
            private string _loaded = "";

            public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
            {
                _loaded = "server data";
                return Task.CompletedTask;
            }

            public override VisualNode Build(ComponentContext context) => new Text(_loaded, TypeRole.BodyM);
        }
    }

    /// <summary>A property that halves what it is given, keeping its store through C#'s <c>field</c>.</summary>
    [Page("/hydration-halves")]
    public sealed class HalvesPage : StatelessComponent, IServerPrefetch
    {
        public int Half { get; set => field = value / 2; }

        public Task PrefetchAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Half = 84;
            return Task.CompletedTask;
        }

        public override VisualNode Build(ComponentContext context) => new Text($"half {Half}", TypeRole.BodyM);
    }

    /// <summary>A primary-constructor parameter a member reads, so the C# compiler gives it a field.</summary>
    public sealed class CapturedProbe(string symbol)
    {
        public string Read() => symbol;
    }

    private static async Task<JsonElement> PayloadOf(string path)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddUI(options => options.ScanAssembly(Assembly.GetExecutingAssembly()));
        var app = builder.Build();
        app.MapUI();
        await app.StartAsync();
        await using var _ = app;
        var html = await app.GetTestClient().GetStringAsync(path);

        var payload = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.Singleline);
        payload.Success.Should().BeTrue("a page with a prefetching component carries its state in the document");
        return JsonDocument.Parse(payload.Groups[1].Value).RootElement.Clone();
    }

    private static string[] Names(JsonElement entry) =>
        entry.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task AFieldAnAutoPropertyAndAPrivateField_CrossUnderTheTwinsNames()
    {
        var state = await PayloadOf("/hydration-names");
        var page = state.GetProperty(eQuantic.UI.Web.ComponentIdentity.Key(typeof(NamesPage), 0));

        Names(page).Should().Equal(["_status", "downloads", "visits"]);
        page.GetProperty("downloads").GetInt32().Should().Be(7);
        page.GetProperty("visits").GetInt32().Should().Be(42);
        page.GetProperty("_status").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task ACapturedParameter_CrossesUnderItsName_WithTheValueThePrefetchLeft()
    {
        var state = await PayloadOf("/hydration-names");
        var quote = state.GetProperty(eQuantic.UI.Web.ComponentIdentity.Key(typeof(Quote), 0));

        Names(quote).Should().Equal(["symbol"]);
        quote.GetProperty("symbol").GetString().Should().Be("ACME");
    }

    [Fact]
    public void ACapturedParameter_IsStoredInTheFieldTheServerReads()
    {
        // The one convention the server keeps: the C# compiler stores a captured primary-constructor
        // parameter in a field named <name>P. Read off a type this compiler built, so a compiler that
        // names it differently fails here, by name, instead of every captured value going missing.
        typeof(CapturedProbe).GetField("<symbol>P", BindingFlags.Instance | BindingFlags.NonPublic)
            .Should().NotBeNull("HydrationContract reads a captured parameter from the field <name>P");
    }

    [Fact]
    public async Task APropertyThatKeepsItsStoreThroughField_CrossesAsTheStore_IntoTheTwinsSlot()
    {
        // Through the setter, the 42 the getter answers would land as 21: the store crosses instead.
        var state = await PayloadOf("/hydration-halves");
        var page = state.GetProperty(eQuantic.UI.Web.ComponentIdentity.Key(typeof(HalvesPage), 0));

        Names(page).Should().Equal(["$half"]);
        page.GetProperty("$half").GetInt32().Should().Be(42);
    }

    [Fact]
    public void AFieldBackedProperty_IsStoredInTheFieldTheServerReads()
    {
        // The C# compiler's name for the store of a property whose accessors use `field`, read off a
        // type it built, as the captured parameter's is above.
        typeof(HalvesPage).GetField("<Half>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .Should().NotBeNull("HydrationContract reads a field-backed property's store from <Name>k__BackingField");
    }

    [Fact]
    public async Task APrivateNestedComponent_CarriesItsState()
    {
        var state = await PayloadOf("/hydration-nested");

        var entry = state.EnumerateObject().Should().ContainSingle().Subject.Value;
        entry.GetProperty("_loaded").GetString().Should().Be("server data");
    }

    [Fact]
    public void ATypeItsAssemblyDoesNotDescribe_HasNoContract()
    {
        // The server writes a component's state only as its assembly's manifest describes it. An
        // assembly built without the generator has none, so a type from it carries nothing; the
        // Material theme stands in, from an assembly no generator ran over.
        eQuantic.UI.Server.Rendering.HydrationContract.For(typeof(eQuantic.UI.Material.MaterialTheme)).Should().BeNull();
    }
}
