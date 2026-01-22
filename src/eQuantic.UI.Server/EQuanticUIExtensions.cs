using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Extension methods for configuring eQuantic.UI in ASP.NET Core.
/// </summary>
public static class EQuanticUIExtensions
{
    /// <summary>
    /// Adds eQuantic.UI services to the DI container.
    /// </summary>
    public static IServiceCollection AddEQuanticUI(this IServiceCollection services, Action<EQuanticUIOptions>? configure = null)
    {
        var options = new EQuanticUIOptions();
        configure?.Invoke(options);
        
        services.AddSingleton(options);
        services.AddSingleton<IServerActionRegistry>(sp =>
        {
            var registry = new ServerActionRegistry();
            
            // Scan registered assemblies
            foreach (var assembly in options.AssembliesToScan)
            {
                registry.ScanAssembly(assembly);
            }
            
            return registry;
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds the Server Actions middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseEQuanticServerActions(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ServerActionsMiddleware>();
    }
    
    /// <summary>
    /// Maps the eQuantic.UI fallback route to serve the SPA HTML shell.
    /// </summary>
    public static IEndpointRouteBuilder MapEQuanticUi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFallback(async context =>
        {
            var options = context.RequestServices.GetRequiredService<EQuanticUIOptions>();
            var shell = options.HtmlShell;
            
            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{shell.Title}</title>
    <style>
        {shell.BaseStyles}
    </style>
    {string.Join("\n    ", shell.HeadTags)}
</head>
<body>
    <div id=""app"">
        <div class=""loading"">Loading...</div>
    </div>
    
    <!-- eQuantic.UI Runtime -->
    <script type=""module"">
        // Placeholder for runtime loading
        console.log('eQuantic.UI Runtime loaded');
    </script>
</body>
</html>";

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        });

        return endpoints;
    }
}

/// <summary>
/// Configuration options for eQuantic.UI.
/// </summary>
public class EQuanticUIOptions
{
    internal List<Assembly> AssembliesToScan { get; } = new();
    
    /// <summary>
    /// Configuration for the HTML shell (index.html).
    /// </summary>
    public HtmlShellOptions HtmlShell { get; } = new();
    
    /// <summary>
    /// Scan an assembly for components with [Page] and [ServerAction] attributes.
    /// </summary>
    public EQuanticUIOptions ScanAssembly(Assembly assembly)
    {
        AssembliesToScan.Add(assembly);
        return this;
    }
}

/// <summary>
/// Options for generating the HTML shell.
/// </summary>
public class HtmlShellOptions
{
    public string Title { get; set; } = "eQuantic.UI App";
    public string BaseStyles { get; set; } = @"
        body { font-family: system-ui, sans-serif; margin: 0; padding: 0; }
        .loading { display: flex; justify-content: center; align-items: center; height: 100vh; font-size: 1.5rem; }
    ";
    public List<string> HeadTags { get; } = new();
}
