using System.Text;
using System.Text.Json;
using eQuantic.UI.Server.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// A server action runs on a component built from THIS REQUEST's services.
/// <para>
/// This is where the scoped things live — the DbContext the action queries, the unit of work it
/// commits, the tenant it belongs to. A middleware is a singleton, so the provider it was
/// constructed with is the ROOT one, and .NET refuses to hand a scoped service out of that: doing
/// so would keep it alive past the request and share it with every later one.
/// </para>
/// </summary>
public class ServerActionScopeTests
{
    private interface ICurrentTenant
    {
        string Name { get; }
    }

    private sealed class Tenant : ICurrentTenant
    {
        public string Name => "acme";
    }

    /// <summary>A page whose action reads a per-request dependency — the ordinary shape.</summary>
    private sealed class TenantActions(ICurrentTenant tenant)
    {
        public string WhoAmI() => tenant.Name;
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(
            HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    private static async Task<(int Status, string Body)> Invoke(ServiceProvider root)
    {
        var registry = new ServerActionRegistry();
        registry.RegisterAction("TenantActions/WhoAmI", new ServerActionDescriptor
        {
            ActionId = "TenantActions/WhoAmI",
            ComponentType = typeof(TenantActions),
            Method = typeof(TenantActions).GetMethod(nameof(TenantActions.WhoAmI))!,
        });

        var options = new UIOptions();
        options.ScanAssembly(typeof(ServerActionScopeTests).Assembly);

        var middleware = new ServerActionsMiddleware(
            next: _ => Task.CompletedTask,
            registry: registry,
            serviceProvider: root,
            authorizationService: new AlwaysAllowed(),
            options: options,
            logger: NullLogger<ServerActionsMiddleware>.Instance);

        using var scope = root.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/api/_equantic/actions";
        context.Request.Method = "POST";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { actionId = "TenantActions/WhoAmI", arguments = Array.Empty<object>() })));
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await middleware.InvokeAsync(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(responseBody.ToArray()));
    }

    [Fact]
    public async Task AnActionTakingAScopedService_Runs()
    {
        // validateScopes: true — what ASP.NET Core builds with in Development, and the only setting
        // under which this says anything. Off (the ServiceCollection default) a scoped service
        // resolves from the root perfectly happily, which is why the bug was invisible.
        var root = new ServiceCollection()
            .AddScoped<ICurrentTenant, Tenant>()
            .BuildServiceProvider(validateScopes: true);

        var (status, body) = await Invoke(root);

        status.Should().Be(StatusCodes.Status200OK, body);
        body.Should().Contain("acme");
    }
}
