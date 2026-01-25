using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Extension methods for configuring eQuantic.UI in ASP.NET Core.
/// </summary>
public static class UIExtensions
{
    // Deterministic Build ID based on Assembly Timestamp
    // This allows browser caching to work across server restarts, 
    // invalidating only when the code actually changes.
    public static readonly string BuildId = GetDeterministicBuildId();

    private static string GetDeterministicBuildId()
    {
        try
        {
            var location = typeof(UIExtensions).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                // Use hex timestamp for a short, unique, and ordered ID
                return System.IO.File.GetLastWriteTimeUtc(location).Ticks.ToString("x");
            }
        }
        catch { /* Fallback to random if file access fails */ }
        
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Adds UI services to the DI container.
    /// </summary>
    public static IServiceCollection AddUI(this IServiceCollection services, Action<UIOptions>? configure = null)
    {
        var options = new UIOptions();
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

        // Add SignalR services
        services.AddSignalR();

        return services;
    }

    /// <summary>
    /// Adds the Server Actions middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseServerActions(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ServerActionsMiddleware>();
    }

    /// <summary>
    /// Maps page routes based on [Page] attributes found in scanned assemblies.
    /// </summary>
    public static IEndpointRouteBuilder MapPages(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<UIOptions>();
        var registry = endpoints.ServiceProvider.GetRequiredService<IServerActionRegistry>();

        // Get all page routes from scanned assemblies
        foreach (var assembly in options.AssembliesToScan)
        {
            var pageTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<Core.PageAttribute>() != null);

            foreach (var pageType in pageTypes)
            {
                var pageAttr = pageType.GetCustomAttribute<Core.PageAttribute>()!;
                var route = pageAttr.Route;

                endpoints.MapGet(route, async context =>
                {
                    await ServeAppShell(context, pageType.Name);
                });
            }
        }

        // Map SignalR Hub
        endpoints.MapHub<Hubs.ServerActionHub>("/_equantic/hub");

        // Map Runtime JS
        endpoints.MapGet("/_equantic/runtime.js", async context =>
        {
            context.Response.ContentType = "application/javascript";
            var assembly = typeof(UIExtensions).Assembly;
            var resourceName = "eQuantic.UI.Server.runtime.js";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream == null)
            {
                context.Response.StatusCode = 404;
                var resources = string.Join(", ", assembly.GetManifestResourceNames());
                await context.Response.WriteAsync($"console.error('Runtime embedded resource not found: {resourceName}. Available: {resources}');");
                return;
            }
            await stream.CopyToAsync(context.Response.Body);
        });

        // Debug/Fallback: Manually serve component files if StaticFiles misses them
        endpoints.MapGet("/_equantic/{name}.js", async context =>
        {
            var name = (string?)context.GetRouteValue("name");
            var path = System.IO.Path.Combine(context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().WebRootPath, "_equantic", $"{name}.js");
            
            if (System.IO.File.Exists(path))
            {
                context.Response.ContentType = "application/javascript";
                await context.Response.SendFileAsync(path);
            }
            else
            {
                context.Response.StatusCode = 404;
                // Try finding it in the local directory (Dev scenario)
                var localPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "_equantic", $"{name}.js");
                 if (System.IO.File.Exists(localPath))
                {
                    context.Response.ContentType = "application/javascript";
                    await context.Response.SendFileAsync(localPath);
                }
                else 
                {
                    await context.Response.WriteAsync($"// 404: Component {name} not found at {path} or {localPath}");
                }
            }
        });

        return endpoints;
    }

    /// <summary>
    /// Maps the UI fallback route to serve the SPA HTML shell.
    /// </summary>
    /// <summary>
    /// Maps the UI fallback route to serve the SPA HTML shell.
    /// Includes mapping of pages and runtime assets.
    /// </summary>
    public static IEndpointRouteBuilder MapUI(this IEndpointRouteBuilder endpoints)
    {
        // Ensure all page routes and assets are mapped
        endpoints.MapPages();

        endpoints.MapFallback(async context =>
        {
            // Fallback for 404s or root
            await ServeAppShell(context, null);
        });

        return endpoints;
    }

    private static async Task ServeAppShell(HttpContext context, string? pageName)
    {
        var options = context.RequestServices.GetRequiredService<UIOptions>();
        var shell = options.HtmlShell;
        var pageValue = pageName != null ? $"'{pageName}'" : "null";

        // Inject configuration object
        var configJson = $@"{{
            page: {pageValue},
            version: '{BuildId}'
        }}";

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
    
    <!-- Import Map for bare modules -->
    <script type=""importmap"">
    {{
        ""imports"": {{
            ""@equantic/runtime"": ""/_equantic/runtime.js?v={BuildId}""
        }}
    }}
    </script>
    
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js""></script>
</head>
<body>
    <div id=""app"">
        <div class=""loading"">Loading...</div>
    </div>

    <!-- eQuantic.UI Runtime (Static Asset) -->
    <script>
        window.__EQ_CONFIG = {configJson};
    </script>
    <script type=""module"">
        import {{ boot }} from ""@equantic/runtime"";
        boot();
    </script>
</body>
</html>";

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
}

/// <summary>
/// Configuration options for UI services.
/// </summary>
public class UIOptions
{
    internal List<Assembly> AssembliesToScan { get; } = new();

    /// <summary>
    /// Configuration for the HTML shell (index.html).
    /// </summary>
    public HtmlShellOptions HtmlShell { get; } = new();

    /// <summary>
    /// Scan an assembly for components with [Page] and [ServerAction] attributes.
    /// </summary>
    public UIOptions ScanAssembly(Assembly assembly)
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
