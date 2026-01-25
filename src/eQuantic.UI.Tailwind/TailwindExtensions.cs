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

        // Register default theme
        // We need to access the IServiceCollection to register services, but IApplicationBuilder doesn't expose it directly.
        // Usually, registration happens in ConfigureServices (AddTailwind). 
        // Since we are in UseTailwind (Configure), we can't register services here.
        // However, we can add it to the RenderContext if we can access it, OR we should have an AddTailwind method.
        
        // Correct approach: Provide AddTailwind method in IServiceCollection extension, or
        // Manual registration if the user already built the provider.
        
        // Providing a workaround: Register instance in UIOptions or similar if global.
        // But for now, let's assume the user will register implementation, OR we provide AddTailwind().
        
        // Let's create an AddTailwind extension method for IServiceCollection instead?
        // The user is asking to "Register Theme in TailwindExtensions.cs". 
        // Existing UIExtensions likely has AddUI.
        
        // If I cannot change Program.cs easily (I can, but I want to minimize changes), 
        // I will assume IAppTheme is accessed via UIOptions or a global service locator if RenderContext doesn't have it.
        
        // WAIT. RenderContext in Card.cs `Build(RenderContext context)`. 
        // Where does `context` come from? 
        // It is passed by the renderer.

        // I will MODIFY TailwindExtensions to include `AddTailwind` for IServiceCollection if it doesn't exist, 
        // OR simply register it here if I check `app.ApplicationServices`. 
        // You cannot add services to IServiceProvider after build.
        
        // So I MUST add `AddTailwind` extension to `IServiceCollection`.
        
        return app;

        // Add the Tailwind link to the head tags
        var version = DateTime.UtcNow.Ticks;
        var linkTag = $"<link rel=\"stylesheet\" href=\"{cssPath}?v={version}\">";
        if (!options.HtmlShell.HeadTags.Any(t => t.StartsWith($"<link rel=\"stylesheet\" href=\"{cssPath}")))
        {
            options.HtmlShell.HeadTags.Add(linkTag);
        }

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
