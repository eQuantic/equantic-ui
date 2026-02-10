using System;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Material.Theme;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Material;

/// <summary>
/// Extension methods for registering Material Design 3 theme services
/// </summary>
public static class MaterialServiceExtensions
{
    /// <summary>
    /// Adds Material Design 3 theme to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMaterialTheme(this IServiceCollection services)
    {
        services.AddSingleton<IAppTheme, MaterialAppTheme>();
        return services;
    }

    /// <summary>
    /// Adds Material Design 3 theme with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for theme options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMaterialTheme(
        this IServiceCollection services,
        Action<MaterialThemeOptions> configure)
    {
        var options = new MaterialThemeOptions();
        configure(options);
        
        services.AddSingleton(options);
        services.AddSingleton<IAppTheme, MaterialAppTheme>();
        return services;
    }

    public static UIOptions UseMaterialTheme(this UIOptions options, Action<MaterialThemeOptions>? configure = null)
    {
        options.RegisterServices(services =>
        {
            if (configure != null)
            {
                services.AddMaterialTheme(configure);
            }
            else
            {
                services.AddMaterialTheme();
            }
        });
        return options;
    }
}

/// <summary>
/// Configuration options for Material Design 3 theme
/// </summary>
public class MaterialThemeOptions
{
    /// <summary>
    /// Source color (hex) for generating the M3 color palette
    /// Default is a blue-purple Material primary color
    /// </summary>
    public string SourceColor { get; set; } = "#6750A4";
    
    /// <summary>
    /// Enable dark mode by default
    /// </summary>
    public bool DarkMode { get; set; } = false;
    
    /// <summary>
    /// Custom font family for typography
    /// </summary>
    public string? FontFamily { get; set; }
}
