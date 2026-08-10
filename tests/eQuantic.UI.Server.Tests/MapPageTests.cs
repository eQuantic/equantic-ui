using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A route declared beside every other endpoint — <c>app.MapPage&lt;HomePage&gt;("/")</c> — instead
/// of on the page itself.
/// <para>
/// The attribute stays and remains the better answer for a page whose route is part of what it IS.
/// This is for the rest: routes an app wants to read in one place, routes that differ between
/// hosts, a page mounted at a path its own file has no business knowing, and pages from an assembly
/// you do not own, which the attribute cannot route at all.
/// </para>
/// </summary>
public class MapPageTests
{
    /// <summary>No [Page] attribute anywhere on it — the route lives in Program.cs.</summary>
    private sealed class UnattributedPage : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text("mapped from Program", TypeRole.Heading);
    }

    private sealed class NotAPage
    {
    }

    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddUI(o => o.ScanAssembly(typeof(MapPageTests).Assembly));
        var app = builder.Build();
        app.MapPage<UnattributedPage>("/mapped", title: "Mapped");
        return app;
    }

    /// <summary>
    /// A route has to register in all THREE places a route exists — the endpoint table, the SSR
    /// page index, and the client's table for SPA navigation. Half-registering is worse than not
    /// registering: the page serves, and then the first client-side link to it reloads the whole
    /// document for no visible reason.
    /// </summary>
    [Fact]
    public async Task AMappedPage_IsRenderableByName()
    {
        await using var app = BuildApp();

        var service = app.Services.GetRequiredService<IServerRenderingService>();
        var context = new DefaultHttpContext { RequestServices = app.Services };

        var result = await service.RenderPageAsync(nameof(UnattributedPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("mapped from Program",
            "the SSR index has to know a page the attribute scan never saw");
    }

    [Fact]
    public async Task AMappedRoute_IsRecordedOnce_ForEveryConsumerToRead()
    {
        await using var app = BuildApp();

        var options = app.Services.GetRequiredService<UIOptions>();

        options.DeclaredRoutes.Should().ContainSingle()
            .Which.Should().Be(("/mapped", typeof(UnattributedPage), "Mapped"));
    }

    /// <summary>A type that is not a component fails HERE, at startup, naming itself — not on the
    /// first request to a route nobody can serve.</summary>
    [Fact]
    public async Task MappingSomethingThatIsNotAPage_SaysSoAtStartup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddUI(o => o.ScanAssembly(typeof(MapPageTests).Assembly));
        await using var app = builder.Build();

        var mapping = () => app.MapPage<NotAPage>("/nope");

        mapping.Should().Throw<ArgumentException>().WithMessage("*NotAPage is not a page*");
    }
}
