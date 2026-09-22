using eQuantic.UI.Images;
using eQuantic.UI.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Images;

/// <summary>
/// Extension methods for configuring image optimization in eQuantic.UI.
/// </summary>
public static class ImageExtensions
{
    /// <summary>
    /// Adds image optimization services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddImageOptimization(
        this IServiceCollection services,
        Action<ImageOptimizationOptions>? configure = null)
    {
        var options = new ImageOptimizationOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<ImageOptimizer>();
        services.AddSingleton<ImageCache>();
        services.AddSingleton<BlurPlaceholderGenerator>();

        return services;
    }

    /// <summary>
    /// Maps the image optimization endpoint and enables global image optimization.
    /// Call after UseStaticFiles() and before MapUI().
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication UseImageOptimization(this WebApplication app)
    {
        // Resolved and dropped ON PURPOSE: this overload is the one an author can reach without
        // having called AddImageOptimization, and a missing registration should stop the app at
        // startup rather than answer the first image request with a 500 from inside the handler.
        app.Services.GetRequiredService<ImageOptimizationOptions>();
        MapImageEndpoint(app);
        return app;
    }

    /// <summary>
    /// Enables server-side image optimization via UIOptions fluent API.
    /// Registers services and the optimization endpoint mapping.
    /// </summary>
    public static UIOptions UseImageOptimization(
        this UIOptions options, 
        Action<ImageOptimizationOptions>? configure = null)
    {
        options.RegisterServices(services => services.AddImageOptimization(configure));
        // No lookup here: the same call registered the services a line above, so there is nothing
        // to check and nothing to hand over.
        options.RegisterEndpoints(MapImageEndpoint);
        return options;
    }

    /// <summary>One owner of the route, which is the only reason this is still a method.</summary>
    private static void MapImageEndpoint(IEndpointRouteBuilder endpoints) =>
        // The endpoint IS the feature: the handler reads ImageOptimizationOptions from the
        // REQUEST's services on every call, so nothing needs handing to it here.
        endpoints.MapGet("/_equantic/image", ImageOptimizationMiddleware.HandleAsync);
}
