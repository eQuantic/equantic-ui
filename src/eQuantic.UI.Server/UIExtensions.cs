using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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

        // Add authorization service for Server Actions
        // TryAdd allows users to override with their own implementation
        services.TryAddSingleton<IServerActionAuthorizationService, ServerActionAuthorizationService>();

        // Add SSR rendering service
        services.TryAddSingleton<IServerRenderingService, ServerRenderingService>();

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
                .Where(t => t.GetCustomAttributes<Core.PageAttribute>().Any());

            foreach (var pageType in pageTypes)
            {
                var pageAttrs = pageType.GetCustomAttributes<Core.PageAttribute>();
                
                foreach (var pageAttr in pageAttrs) 
                {
                    var route = pageAttr.Route;

                    endpoints.MapGet(route, async context =>
                    {
                        await ServeAppShell(context, pageType.Name);
                    });
                }
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

        // Initialize Metadata
        var metadata = new MetadataCollection { Title = shell.Title };
        var seo = new SeoBuilder(metadata);

        // Attempt SSR if page name is provided and SSR is enabled
        var ssrContent = "<div class=\"loading\">Loading...</div>";
        var ssrEnabled = false;

        if (pageName != null && options.EnableSsr)
        {
            var renderingService = context.RequestServices.GetService<IServerRenderingService>();
            if (renderingService != null)
            {
                try
                {
                    var result = await renderingService.RenderPageAsync(pageName, context);
                    if (result.Success && result.Html != null)
                    {
                        ssrContent = result.Html;
                        ssrEnabled = true;

                        // Merge metadata from SSR
                        if (result.Metadata != null)
                        {
                            if (!string.IsNullOrEmpty(result.Metadata.Title))
                                metadata.Title = result.Metadata.Title;
                            
                            foreach(var tag in result.Metadata.Tags)
                                metadata.AddOrUpdate(tag);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetService<ILogger<UIOptions>>();
                    logger?.LogWarning(ex, "SSR failed for page {PageName}, falling back to client-side rendering", pageName);
                }
            }
        }

        // Apply PageAttribute metadata if not already set by SSR
        if (pageName != null)
        {
            var pageType = options.AssembliesToScan
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == pageName && t.GetCustomAttributes<Core.PageAttribute>().Any());

            if (pageType != null)
            {
                var attr = pageType.GetCustomAttributes<Core.PageAttribute>().FirstOrDefault()!;
                if (!string.IsNullOrEmpty(attr.Title) && string.IsNullOrEmpty(metadata.Title))
                    seo.Title(attr.Title);
                
                if (!string.IsNullOrEmpty(attr.Description) && !metadata.Tags.Any(t => t.Key == "name:description"))
                    seo.Description(attr.Description);
            }
        }

        // Inject configuration object
        var configJson = $@"{{
            page: {pageValue},
            version: '{BuildId}',
            ssr: {ssrEnabled.ToString().ToLowerInvariant()}
        }}";

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{System.Web.HttpUtility.HtmlEncode(metadata.Title)}</title>
    {metadata.RenderTags()}
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
    <div id=""app"" data-ssr=""{ssrEnabled.ToString().ToLowerInvariant()}"">
        {ssrContent}
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
    /// Enables Server-Side Rendering (SSR) for SEO optimization.
    /// When enabled, pages will be pre-rendered on the server and sent as HTML.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// SSR provides:
    /// - Better SEO (search engines can index the content)
    /// - Faster First Contentful Paint (FCP)
    /// - Social media preview cards (Open Graph)
    ///
    /// Individual pages can opt-out using [Page(DisableSsr = true)].
    /// </remarks>
    public bool EnableSsr { get; set; } = true;

    public UIOptions WithSsr(bool enabled = true)
    {
        EnableSsr = enabled;
        return this;
    }

    public UIOptions ConfigureHtmlShell(Action<HtmlShellOptions> configure)
    {
        configure(HtmlShell);
        return this;
    }

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

    public HtmlShellOptions SetTitle(string title)
    {
        Title = title;
        return this;
    }

    public HtmlShellOptions AddHeadTag(string tag)
    {
        HeadTags.Add(tag);
        return this;
    }

    public HtmlShellOptions SetBaseStyles(string styles)
    {
        BaseStyles = styles;
        return this;
    }
}
