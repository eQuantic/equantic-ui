using System.Text;
using System.Text.Json;
using Primitives = eQuantic.UI.Primitives;
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

    /// <summary>
    /// The page that asks for what it needs INSIDE the action instead of through its constructor.
    /// Both shapes have to work, and until the middleware armed the capability scope only one did:
    /// a page had to choose between reaching its services here and keeping its prefetched state,
    /// because the very constructor injection that bought the first used to silence the second.
    /// </summary>
    private sealed class AmbientActions : Primitives.StatelessComponent
    {
        public string WhoAmI() => GetService<ICurrentTenant>()?.Name ?? "(nothing armed a resolver)";

        public override Primitives.VisualNode Build(Primitives.ComponentContext context) =>
            new Primitives.Text("unused", Primitives.TypeRole.BodyM);
    }

    private sealed class AlwaysAllowed : IServerActionAuthorizationService
    {
        public Task<ServerActionAuthorizationResult> AuthorizeAsync(
            HttpContext context, ServerActionDescriptor descriptor) =>
            Task.FromResult(ServerActionAuthorizationResult.Success());
    }

    private static async Task<(int Status, string Body)> Invoke(
        ServiceProvider root, Type component = null!, string method = "WhoAmI")
    {
        component ??= typeof(TenantActions);
        var actionId = $"{component.Name}/{method}";

        var registry = new ServerActionRegistry();
        registry.RegisterAction(actionId, new ServerActionDescriptor
        {
            ActionId = actionId,
            ComponentType = component,
            Method = component.GetMethod(method)!,
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
            JsonSerializer.Serialize(new { actionId, arguments = Array.Empty<object>() })));
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

    [Fact]
    public async Task AnActionResolvingFromTheAmbientScope_ReachesTheRequestContainer()
    {
        var root = new ServiceCollection()
            .AddScoped<ICurrentTenant, Tenant>()
            .BuildServiceProvider(validateScopes: true);

        var (status, body) = await Invoke(root, typeof(AmbientActions));

        // GetService<T>() answers "from anywhere in the component" — a lifecycle hook, an event
        // handler, Build itself. An action is anywhere. It used to return null here, and the page
        // could not tell a missing registration from a scope nobody armed.
        status.Should().Be(StatusCodes.Status200OK, body);
        body.Should().Contain("acme");
    }

    [Fact]
    public async Task TheAmbientScope_DoesNotOutliveTheAction()
    {
        var root = new ServiceCollection()
            .AddScoped<ICurrentTenant, Tenant>()
            .BuildServiceProvider(validateScopes: true);

        await Invoke(root, typeof(AmbientActions));

        // The request's container is gone once the request is; leaving it armed would let the NEXT
        // caller resolve scoped services out of a scope that has already been disposed.
        Primitives.CapabilityScope.Current.Should().BeNull();
    }
}
