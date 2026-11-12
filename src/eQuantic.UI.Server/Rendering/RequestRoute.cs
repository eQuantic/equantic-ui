using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// The REQUEST's route, as a service of the request, for code outside the tree that needs it: a
/// prefetch is handed the request's services and resolves <see cref="Primitives.RouteValues"/> from
/// them. A component reads the same route from its context, which the server hands the owner it draws
/// with (#1395). It replaced an ambient channel every render armed and cleared.
/// </summary>
internal static class RequestRoute
{
    /// <summary>Registers the request's route, read from the request ASP.NET is serving.</summary>
    internal static IServiceCollection AddRequestRoute(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped(provider =>
            provider.GetRequiredService<IHttpContextAccessor>().HttpContext is { } request
                ? ServerRenderingService.BuildRouteValues(request)
                : Primitives.RouteValues.Empty);
        return services;
    }
}
