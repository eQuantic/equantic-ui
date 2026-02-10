using eQuantic.UI.Core.Images;
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
        var options = app.Services.GetRequiredService<ImageOptimizationOptions>();

        // Map the image optimization endpoint
        app.MapGet("/_equantic/image", ImageOptimizationMiddleware.HandleAsync);

        // Set global state so the Image component knows optimization is available
        ImageOptimizationState.IsEnabled = true;
        ImageOptimizationState.DefaultQuality = options.DefaultQuality;
        ImageOptimizationState.DeviceSizes = options.DeviceSizes;
        ImageOptimizationState.ImageSizes = options.ImageSizes;

        return app;
    }
}
