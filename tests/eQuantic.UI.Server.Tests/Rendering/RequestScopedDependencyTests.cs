using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using StatelessComponent = eQuantic.UI.Primitives.StatelessComponent;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// A page is built from THIS REQUEST's services.
/// <para>
/// The distinction is not academic: a scoped registration is most of what a page actually depends
/// on — the DbContext it queries, the unit of work it commits, the tenant it belongs to — and .NET
/// refuses to resolve any of them from the root provider, because a scoped service resolved there
/// outlives the request and is then shared by every later one.
/// </para>
/// </summary>
public class RequestScopedDependencyTests
{
    /// <summary>The shape every real per-request dependency has.</summary>
    private interface ICurrentTenant
    {
        string Name { get; }
    }

    private sealed class Tenant : ICurrentTenant
    {
        public string Name => "acme";
    }

    [Page("/scoped-dependency-test")]
    private sealed class TenantPage(ICurrentTenant tenant) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text($"Tenant: {tenant.Name}", TypeRole.Heading);
    }

    /// <summary>A page whose own constructor throws — the failure the bare catch used to erase.</summary>
    [Page("/broken-constructor-test")]
    private sealed class BrokenPage : StatelessComponent
    {
        public BrokenPage()
        {
            throw new InvalidOperationException("the page's own constructor is wrong");
        }

        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    /// <summary>The page that made this necessary: it takes a browser capability, and it has to be
    /// server-renderable anyway.</summary>
    [Page("/capability-test")]
    private sealed class StoragePage(IAppStorage storage) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Text(storage.Get("greeting") ?? "(nothing stored)", TypeRole.Heading);
    }

    /// <summary>
    /// A page taking a DEVICE capability renders on the server. Without a registration there is no
    /// constructor the container can satisfy, so the request ends in a 500 — meaning the one page
    /// that does something is the one page a crawler never sees, and the visitor waits for
    /// JavaScript just to be told the page exists.
    /// <para>
    /// It resolves to an ABSENT one, not a simulated one: there is no camera in a datacenter and
    /// the visitor's localStorage is on the visitor's machine. So the page takes the branch it
    /// already has to have, and the client boot's real capability replaces it on the first client
    /// render.
    /// </para>
    /// </summary>
    [Fact]
    public async Task APageTakingADeviceCapability_StillRendersOnTheServer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUI(o => o.ScanAssembly(typeof(RequestScopedDependencyTests).Assembly));
        var root = services.BuildServiceProvider(validateScopes: true);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var service = scope.ServiceProvider.GetRequiredService<IServerRenderingService>();

        var result = await service.RenderPageAsync(nameof(StoragePage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("(nothing stored)",
            "the server has nothing to hand back, and says so instead of pretending");
    }

    private static ServerRenderingService CreateService(out ServiceProvider root)
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(RequestScopedDependencyTests).Assembly);
        // validateScopes: true — what ASP.NET Core itself builds with in Development, and the only
        // setting under which this test says anything. Off (the ServiceCollection default) a scoped
        // service resolves from the root perfectly happily, which is exactly why the bug shipped:
        // it is invisible until the container is asked to check.
        root = new ServiceCollection()
            .AddScoped<ICurrentTenant, Tenant>()
            .BuildServiceProvider(validateScopes: true);
        return new ServerRenderingService(root, options, NullLogger<ServerRenderingService>.Instance);
    }

    [Fact]
    public async Task APageTakingAScopedService_IsBuilt()
    {
        var service = CreateService(out var root);
        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        var result = await service.RenderPageAsync(nameof(TenantPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Tenant: acme",
            "the dependency has to come from the request's container — the root cannot give it at all");
    }

    /// <summary>
    /// A page whose constructor throws says so. The catch here used to be bare: the DI attempt
    /// failed for ANY reason, the page was quietly rebuilt with nothing injected, and it rendered
    /// its empty state as if it had asked for nothing. The dependency was null and the exception
    /// that explained why was gone — which is the hardest kind of bug to find, because the page
    /// looks like it is working.
    /// </summary>
    [Fact]
    public async Task APageWhoseConstructorThrows_DoesNotComeUpEmpty()
    {
        var service = CreateService(out var root);
        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        var result = await service.RenderPageAsync(nameof(BrokenPage), context);

        result.Success.Should().BeFalse("the page could not be built");
        result.Error.Should().Contain("the page's own constructor is wrong");
    }
}
