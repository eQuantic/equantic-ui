using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

/// <summary>
/// A component in the MIDDLE of a tree finds a capability on its own.
/// <para>
/// A page takes what it needs through its constructor, and everything below it had to be handed the
/// same thing by hand: a card with a "Copy" button needs an ITextClipboard, so the article above it
/// carried one it never used, and the section above that carried it too. A component that gained a
/// need forced an edit in every ancestor between it and the page.
/// </para>
/// </summary>
namespace eQuantic.UI.Server.Tests.Rendering;

public class NestedCapabilityTests
{
    private interface IClipboardish
    {
        string Read();
    }

    private sealed class Clipboard : IClipboardish
    {
        public string Read() => "from the container";
    }

    /// <summary>Three levels down, and nothing above it mentions the capability.</summary>
    private sealed class Leaf : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text(context.GetService<IClipboardish>()?.Read() ?? "(none)", TypeRole.BodyM);
    }

    private sealed class Middle : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Column(gap: Space.S2) { new Leaf() };
    }

    [Page("/nested-capability-test")]
    private sealed class NestedPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Column(gap: Space.S2) { new Middle() };
    }

    [Fact]
    public async Task ANestedComponent_ResolvesItsOwnCapability()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IClipboardish, Clipboard>();
        services.AddUI(o => o.ScanAssembly(typeof(NestedCapabilityTests).Assembly));
        var root = services.BuildServiceProvider(validateScopes: true);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var service = scope.ServiceProvider.GetRequiredService<IServerRenderingService>();

        var result = await service.RenderPageAsync(nameof(NestedPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("from the container",
            "the leaf asked, and nothing between it and the page mentions the capability");
    }

    /// <summary>
    /// The REQUEST's container, so a scoped registration resolves — which is most of what a real
    /// capability is — and a page's own registrations win over the application's.
    /// </summary>
    [Fact]
    public async Task ItResolvesFromTheRequest_NotTheApplication()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IClipboardish, Clipboard>();
        services.AddUI(o => o.ScanAssembly(typeof(NestedCapabilityTests).Assembly));
        var root = services.BuildServiceProvider(validateScopes: true);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        var result = await scope.ServiceProvider.GetRequiredService<IServerRenderingService>()
            .RenderPageAsync(nameof(NestedPage), context);

        result.Success.Should().BeTrue(result.Error);
    }

    /// <summary>The scope does not OUTLIVE the request — SSR renders concurrent requests on shared
    /// state, and a resolver left behind serves one visitor's container to the next.</summary>
    [Fact]
    public async Task TheResolverDoesNotSurviveTheRequest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IClipboardish, Clipboard>();
        services.AddUI(o => o.ScanAssembly(typeof(NestedCapabilityTests).Assembly));
        var root = services.BuildServiceProvider(validateScopes: true);

        using var scope = root.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IServerRenderingService>()
            .RenderPageAsync(nameof(NestedPage), new DefaultHttpContext { RequestServices = scope.ServiceProvider });

        CapabilityScope.Resolve<IClipboardish>().Should().BeNull();
    }
}
