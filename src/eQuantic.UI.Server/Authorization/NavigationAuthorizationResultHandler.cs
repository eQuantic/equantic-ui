using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// How a refused CLIENT NAVIGATION is answered (#673). A navigation is a fetch to the page's own route
/// carrying <c>X-EQ-Navigate</c>, and a fetch cannot follow a scheme's challenge (an OpenID Connect
/// redirect is to another origin), so it is answered 401 when the visitor is not signed in and 403
/// when they are and lack the policy, marked with the header every navigation answer carries. The
/// router then loads the page in full, which the framework's own handling challenges or forbids.
/// <para>
/// Every other request goes to the handler the app had: its own when it registered one before
/// <c>AddUI</c>, the framework's otherwise. One it registers after <c>AddUI</c> replaces this one,
/// and answers a refused navigation itself.
/// </para>
/// </summary>
internal sealed class NavigationAuthorizationResultHandler(IAuthorizationMiddlewareResultHandler inner)
    : IAuthorizationMiddlewareResultHandler
{
    /// <summary>The key the app's own handler is kept under, so the container still builds and disposes it.</summary>
    private static readonly object InnerKey = new();

    /// <summary>
    /// Puts this handler in front of the one the app registered, keeping that one's lifetime and the
    /// container's ownership of it: a scoped handler with a scoped dependency is resolved from the
    /// request's scope and disposed with it (found by Copilot on #686). With none registered, the
    /// framework's handler is the inner one.
    /// </summary>
    public static void Decorate(IServiceCollection services)
    {
        var app = services.LastOrDefault(d =>
            d.ServiceType == typeof(IAuthorizationMiddlewareResultHandler) && !d.IsKeyedService);
        if (app is null)
        {
            services.AddSingleton<IAuthorizationMiddlewareResultHandler>(_ =>
                new NavigationAuthorizationResultHandler(new AuthorizationMiddlewareResultHandler()));
            return;
        }

        services.Remove(app);
        services.Add(app switch
        {
            { ImplementationInstance: { } instance } =>
                new ServiceDescriptor(typeof(IAuthorizationMiddlewareResultHandler), InnerKey, instance),
            { ImplementationFactory: { } factory } =>
                new ServiceDescriptor(typeof(IAuthorizationMiddlewareResultHandler), InnerKey,
                    (provider, _) => factory(provider), app.Lifetime),
            _ => new ServiceDescriptor(typeof(IAuthorizationMiddlewareResultHandler), InnerKey,
                app.ImplementationType!, app.Lifetime),
        });
        services.Add(new ServiceDescriptor(typeof(IAuthorizationMiddlewareResultHandler),
            provider => new NavigationAuthorizationResultHandler(
                provider.GetRequiredKeyedService<IAuthorizationMiddlewareResultHandler>(InnerKey)),
            app.Lifetime));
    }

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded || !context.Request.Headers.ContainsKey(UIExtensions.NavigationHeader))
            return inner.HandleAsync(next, context, policy, authorizeResult);

        context.Response.StatusCode = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;
        context.Response.Headers[UIExtensions.NavigationHeader] = "1";
        return Task.CompletedTask;
    }
}
