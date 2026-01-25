using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Tailwind;

/// <summary>
/// Extension methods for enabling Tailwind CSS in eQuantic.UI.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>
    /// Enables Tailwind CSS integration by adding the default stylesheet link to the HTML shell.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="cssPath">The path to the generated CSS file. Defaults to "/css/app.css".</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseTailwind(this IApplicationBuilder app, string cssPath = "/css/app.css")
    {
        var options = app.ApplicationServices.GetService<UIOptions>();
        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseTailwind().");
        }

        // Add the Tailwind link to the head tags
        var version = DateTime.UtcNow.Ticks;
        var linkTag = $"<link rel=\"stylesheet\" href=\"{cssPath}?v={version}\">";
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }

        // Inject AppTheme as JS service
        var theme = new eQuantic.UI.Tailwind.Theme.AppTheme();
        
        // Use default (PascalCase) naming policy to match C# -> JS transpilation
        var jsonOptions = new System.Text.Json.JsonSerializerOptions 
        { 
            PropertyNamingPolicy = null, // Was CamelCase, now null to keep PascalCase
            WriteIndented = false
        };
        var themeJson = System.Text.Json.JsonSerializer.Serialize(theme, jsonOptions);

        // Register with both Short Name and Full Name to ensure compatibility with C# Compiler output
        var script = $@"
<script type=""module"">
    import {{ getRootServiceProvider }} from '/_equantic/runtime.js?v={version}';
    const theme = {themeJson};
    getRootServiceProvider().registerInstance('IAppTheme', theme);
    getRootServiceProvider().registerInstance('eQuantic.UI.Core.Theme.IAppTheme', theme);
</script>";
        options.HtmlShell.HeadTags.Add(script);

        return app;
    }

    /// <summary>
    /// Registers Tailwind CSS services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services)
    {
        services.AddSingleton<eQuantic.UI.Core.Theme.IAppTheme, eQuantic.UI.Tailwind.Theme.AppTheme>();
        return services;
    }
}
