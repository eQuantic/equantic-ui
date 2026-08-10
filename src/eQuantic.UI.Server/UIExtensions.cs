using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Core.Assets;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    /// <summary>
    /// The id every served bundle carries in its URL, so a browser may cache it forever and still
    /// pick up a new one the moment it exists.
    /// <para>
    /// It has to move when the APP moves. Reading only this assembly's timestamp meant the id
    /// stayed frozen through every change a developer actually makes — their own pages — and the
    /// browser went on serving the JavaScript from before the edit: the page renders, the handlers
    /// are the old ones, and nothing says so. The app's assembly is written by the same build that
    /// writes the bundles, so the LATER of the two is the honest answer.
    /// </para>
    /// </summary>
    private static string GetDeterministicBuildId()
    {
        try
        {
            var stamps = new[] { Assembly.GetEntryAssembly(), typeof(UIExtensions).Assembly }
                .Select(assembly => assembly?.Location)
                .Where(location => !string.IsNullOrEmpty(location) && File.Exists(location))
                .Select(location => File.GetLastWriteTimeUtc(location!).Ticks)
                .ToArray();
            if (stamps.Length > 0) return stamps.Max().ToString("x");
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

        // Execute package service registrations
        foreach (var registration in options.ServiceRegistrations)
        {
            registration(services);
        }

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
        // The server half of the light/dark hand. TryAdd, so an app that registers its own — one
        // that reads a cookie, say — keeps it. Without this a toggle resolved nothing during SSR
        // and had to guess the mode it was drawing.
        services.TryAddSingleton<eQuantic.UI.Primitives.IThemeController>(
            // Unset means "follow the OS", which a server cannot resolve — it reports Light, and
            // the browser's controller settles it at hydration.
            _ => new SsrThemeController(options.InitialThemeMode ?? eQuantic.UI.Primitives.ThemeMode.Light));

        // Add response compression (Brotli + Gzip for JS, CSS, HTML)
        services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/javascript",
                "text/css",
                "image/svg+xml"
            });
        });
        services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);

        // Add SignalR services
        services.AddSignalR();

        // Register explicit asset providers first (WithAssetProvider<T> takes priority)
        foreach (var (serviceType, implType) in options.AssetProviders)
        {
            services.TryAddSingleton(serviceType, implType);
        }

        // Auto-register IComponentAssetProvider<T> implementations from scanned assemblies
        // TryAdd ensures explicit registrations above are not overridden
        var providerInterfaceBase = typeof(IComponentAssetProvider<>);
        foreach (var assembly in options.AssembliesToScan)
        {
            try
            {
                foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
                {
                    foreach (var iface in type.GetInterfaces())
                    {
                        if (iface.IsGenericType && iface.GetGenericTypeDefinition() == providerInterfaceBase)
                        {
                            services.TryAddSingleton(iface, type);
                        }
                    }
                }
            }
            catch
            {
                // Skip assemblies that fail to load types
            }
        }

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

        // Map Runtime JS (immutable via BuildId in URL, long cache)
        endpoints.MapGet("/_equantic/runtime.js", async context =>
        {
            context.Response.ContentType = "application/javascript";
            context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
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
            var path = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "_equantic", $"{name}.js");

            if (File.Exists(path))
            {
                context.Response.ContentType = "application/javascript";
                // Hot reload rewrites fixed-name bundles in place — immutable caching would pin the
                // browser to the pre-edit code forever. Dev revalidates; prod stays immutable.
                var uiOptions = context.RequestServices.GetRequiredService<UIOptions>();
                var dev = uiOptions.HotReload
                    ?? context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
                context.Response.Headers["Cache-Control"] = dev
                    ? "no-cache"
                    : "public, max-age=31536000, immutable";
                await context.Response.SendFileAsync(path);
            }
            else
            {
                context.Response.StatusCode = 404;
                // Try finding it in the local directory (Dev scenario)
                var localPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "_equantic", $"{name}.js");
                 if (File.Exists(localPath))
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

        // Debug/Fallback: serve component source maps so C# breakpoints bind — including on pages reached
        // via client-side (SPA) navigation, where the page bundle is dynamically imported. Without this,
        // the `.js` loads but its `.js.map` 404s and the debugger can't map back to C#. Not cached
        // immutably (the map URL carries no version query, so a stale map must be revalidated).
        endpoints.MapGet("/_equantic/{name}.js.map", async context =>
        {
            var name = (string?)context.GetRouteValue("name");
            var webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var candidates = new[]
            {
                Path.Combine(webRoot, "_equantic", $"{name}.js.map"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "_equantic", $"{name}.js.map"),
            };
            var mapPath = candidates.FirstOrDefault(File.Exists);

            if (mapPath != null)
            {
                context.Response.ContentType = "application/json";
                context.Response.Headers["Cache-Control"] = "no-cache";
                await context.Response.SendFileAsync(mapPath);
            }
            else
            {
                context.Response.StatusCode = 404;
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
        var options = endpoints.ServiceProvider.GetRequiredService<UIOptions>();

        // Phase 3 hot reload — DEVELOPMENT only unless forced: watch sources, re-run the SDK's
        // eqc target, notify browsers over SSE (the runtime replays live state after the reload).
        var environment = endpoints.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        if (options.HotReload ?? environment.IsDevelopment())
        {
            var hotReload = new HotReload.HotReloadService(environment.ContentRootPath);
            hotReload.Start();
            endpoints.MapGet("/_equantic/hmr", hotReload.HandleClient);

            // The stage-one source maps (TS intermediate → C#, C# text embedded) for the error
            // overlay's second hop. Name-only — no separators survive the check, so nothing above
            // obj/eQuantic/ts is reachable. 404s in production along with the whole dev block.
            endpoints.MapGet("/_equantic/src-map/{name}", async context =>
            {
                var name = context.Request.RouteValues["name"] as string ?? "";
                var valid = name.EndsWith(".ts.map", StringComparison.Ordinal)
                    && !name.Contains('/') && !name.Contains('\\') && !name.Contains("..");
                var path = valid
                    ? Path.Combine(environment.ContentRootPath, "obj", "eQuantic", "ts", name)
                    : null;
                if (path is null || !File.Exists(path))
                {
                    context.Response.StatusCode = 404;
                    return;
                }
                context.Response.ContentType = "application/json";
                context.Response.Headers.CacheControl = "no-cache";
                await context.Response.SendFileAsync(path);
            });
        }

        // Apply package endpoint configurations
        foreach (var configuration in options.EndpointConfigurations)
        {
            configuration(endpoints);
        }

        // Ensure all page routes and assets are mapped
        endpoints.MapPages();

        endpoints.MapFallback(async context =>
        {
            // An unknown route IS a 404, and the status code says so unconditionally — a 200 that
            // merely looks like a 404 teaches every crawler and monitor that the page exists.
            // ServeAppShell(null) then resolves options.NotFoundPageType (registered by
            // ScanAssembly when the app declares a `[Page("/404")]`) and boots that page as the
            // content; absent one, the shell boots and the runtime paints its styled default.
            context.Response.StatusCode = 404;
            await ServeAppShell(context, null);
        });

        return endpoints;
    }

    /// <summary>The head links for the generated icon set — empty when an app declared no icon.</summary>
    private static IReadOnlyList<string> GeneratedIconTags(HttpContext context)
    {
        if (_iconTags is not null) return _iconTags;

        var environment = context.RequestServices.GetService<IWebHostEnvironment>();
        var root = environment?.WebRootPath;
        var icons = root is null ? null : Path.Combine(root, "_equantic", "icons");
        if (icons is null || !Directory.Exists(icons)) return _iconTags = [];

        var tags = new List<string>();
        void Link(string file, HtmlTag tag)
        {
            if (File.Exists(Path.Combine(icons, file))) tags.Add(tag.Render());
        }

        // 32 is the tab, 180 is what iOS Safari pins to a home screen, and the manifest carries the
        // install icons — each answers a different question, so each gets its own link.
        Link("favicon-32.png", HtmlTag.Link("icon", "/_equantic/icons/favicon-32.png").Attr("type", "image/png"));
        Link("apple-touch-icon.png", HtmlTag.Link("apple-touch-icon", "/_equantic/icons/apple-touch-icon.png"));
        Link("site.webmanifest", HtmlTag.Link("manifest", "/_equantic/icons/site.webmanifest"));
        return _iconTags = tags;
    }

    /// <summary>Computed once: the icon set is a build output and cannot change under a running app.</summary>
    private static IReadOnlyList<string>? _iconTags;

    private static async Task ServeAppShell(HttpContext context, string? pageName)
    {
        var options = context.RequestServices.GetRequiredService<UIOptions>();
        var shell = options.HtmlShell;
        // Per-request copy of the head tags. The HtmlShell is a singleton, so mutating
        // shell.HeadTags directly is not thread-safe and leaks tags across requests.
        var headTags = new List<string>(shell.HeadTags);
        var pageValue = pageName != null ? $"'{pageName}'" : "null";

        // Write-once theme selection (options.UseTheme): emit the selected theme's NORMATIVE token
        // stylesheet, and serialize it into window.__EQ_THEME__ so boot can setPhotonTheme before
        // hydration (SSR markup + client re-renders then resolve the same colors/shape). Absent a
        // selection, nothing is injected — the runtime keeps its baked-in photonTheme default.
        string? themeDataJson = null;
        if (options.Theme is { } appTheme)
        {
            headTags.Add($"<style>{eQuantic.UI.Web.PhotonCssGenerator.Generate(appTheme, options.InitialThemeMode)}</style>");
            themeDataJson = eQuantic.UI.Web.ThemeBridge.SerializeJson(appTheme);
        }

        // The app icon the SDK generated, linked without the app saying so. Stating it once — in
        // Assets/ — and having it appear in the tab, on a pinned home screen and in the install
        // manifest is the whole point; asking an author to also write four <link> tags would be
        // handing back the work we just took.
        headTags.AddRange(GeneratedIconTags(context));

        // Initialize Metadata
        var metadata = new MetadataCollection { Title = shell.Title };
        var seo = new SeoBuilder(metadata);

        // Attempt SSR if page name is provided and SSR is enabled
        var ssrContent = "<div class=\"loading\">Loading...</div>";
        var ssrEnabled = false;
        string? serializedState = null;

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
                        serializedState = result.SerializedState;

                        // Merge metadata from SSR
                        if (result.Metadata != null)
                        {
                            if (!string.IsNullOrEmpty(result.Metadata.Title))
                                metadata.Title = result.Metadata.Title;

                            foreach(var tag in result.Metadata.Tags)
                                metadata.AddOrUpdate(tag);
                        }

                        // Merge asset dependencies from components
                        if (result.Assets != null)
                        {
                            foreach (var tag in result.Assets.RenderTags())
                            {
                                if (!headTags.Contains(tag))
                                    headTags.Add(tag);
                            }
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
        else
        {
            // 404 Not Found Handling — the fallback endpoint already set the status; here the
            // app's registered /404 page (if any) takes over the CONTENT.
            if (options.NotFoundPageType != null)
            {
                pageName = options.NotFoundPageType.Name;
                pageValue = $"'{pageName}'";
                
                // Try to render the 404 page via SSR
                if (options.EnableSsr)
                {
                    try 
                    {
                        var renderingService = context.RequestServices.GetService<IServerRenderingService>();
                        if (renderingService != null)
                        {
                            var result = await renderingService.RenderPageAsync(pageName, context);
                            if (result.Success && result.Html != null)
                            {
                                ssrContent = result.Html;
                                ssrEnabled = true;
                                if (!string.IsNullOrEmpty(result.Metadata?.Title)) metadata.Title = result.Metadata.Title;
                            }
                        }
                    }
                    catch { /* Ignore 404 render errors */ }
                }
            }
        }

        // 500 Error Handling during SSR
        if (!ssrEnabled && pageName != null && options.EnableSsr)
        {
            // If we are here, it means SSR failed or was disabled.
            // We need to check if it failed due to an exception (which we can't easily track from here without earlier logic change)
            // or if we should show the 500 page.
            
            // Actually, the SSR catch block above sets ssrEnabled = false.
            // If we are in Production and SSR failed, we should render the 500 page.
            var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (!isDev && options.ErrorPageType != null && ssrContent.Contains("Loading..."))
            {
                // Re-attempt SSR with the 500 page
                try
                {
                    var errorPageName = options.ErrorPageType.Name;
                    var renderingService = context.RequestServices.GetService<IServerRenderingService>();
                    if (renderingService != null)
                    {
                         var result = await renderingService.RenderPageAsync(errorPageName, context);
                         if (result.Success && result.Html != null)
                         {
                             context.Response.StatusCode = 500;
                             pageName = errorPageName;
                             pageValue = $"'{pageName}'";
                             ssrContent = result.Html;
                             ssrEnabled = true; // Enabled for the error page
                             if (!string.IsNullOrEmpty(result.Metadata?.Title)) metadata.Title = result.Metadata.Title;
                         }
                    }
                }
                catch { /* Fallback to client-side error */ }
            }
        }

        // Client route table from [Page] attributes — lets the runtime resolve URLs to page bundles
        // for client-side (SPA) navigation without a server round-trip.
        static string JsStr(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");
        var routeEntries = options.AssembliesToScan
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetCustomAttributes<Core.PageAttribute>()
                .Select(attr => (Pattern: attr.Route, Page: t.Name, attr.Title)))
            .Distinct()
            .ToList();
        var routesJson = "[" + string.Join(",", routeEntries.Select(r =>
        {
            var title = string.IsNullOrEmpty(r.Title) ? "" : $",title:'{JsStr(r.Title)}'";
            return $"{{pattern:'{JsStr(r.Pattern)}',page:'{JsStr(r.Page)}'{title}}}";
        })) + "]";

        // Inject configuration object
        var configJson = $@"{{
            page: {pageValue},
            version: '{BuildId}',
            ssr: {ssrEnabled.ToString().ToLowerInvariant()},
            routes: {routesJson}
        }}";

        // Render HTML using template engine with conditionals
        var isDevelopment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        // Check if any component uses Server Actions
        var hasServerActions = options.AssembliesToScan
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods())
            .Any(m => m.GetCustomAttributes(typeof(Core.ServerActionAttribute), false).Any());

        var template = HtmlTemplateEngine.FromResource("eQuantic.UI.Server.Templates.app-shell.html");
        var html = template.Render(ctx =>
        {
            // Variables
            ctx.Set("HtmlClass", shell.HtmlClass)
               .Set("Title", System.Web.HttpUtility.HtmlEncode(metadata.Title))
               .Set("MetadataTags", metadata.RenderTags())
               .Set("BuildId", BuildId)
               .SetOrEmpty("BaseStyles", shell.BaseStyles)
               .Set("HeadTags", string.Join("\n    ", headTags))
               .Set("SsrEnabled", ssrEnabled.ToString().ToLowerInvariant())
               .Set("SsrContent", ssrContent)
               .Set("ConfigJson", configJson)
               .Set("IsDevelopmentBool", isDevelopment ? "true" : "false")
               .SetOrEmpty("InitialState", serializedState != null ? $"window.__INITIAL_STATE__ = {serializedState};" : null)
               .SetOrEmpty("ThemeData", themeDataJson != null ? $"window.__EQ_THEME__ = {themeDataJson};" : null);

            // Conditions
            ctx.When("IsDevelopment", isDevelopment)
               .When("HasInitialState", serializedState != null)
               .When("SsrEnabled", ssrEnabled)
               .When("HasServerActions", hasServerActions)
               ;
        });

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
    /// Additional assemblies whose complex types are permitted as Server Action parameters,
    /// beyond the scanned application assemblies. Use this to opt in DTOs declared in shared
    /// libraries. Framework/system/third-party types are otherwise rejected by default.
    /// </summary>
    public HashSet<Assembly> AllowedDeserializationAssemblies { get; } = new();

    internal List<(Type ServiceType, Type ImplementationType)> AssetProviders { get; } = new();
    internal List<Action<IServiceCollection>> ServiceRegistrations { get; } = new();
    internal List<Action<IEndpointRouteBuilder>> EndpointConfigurations { get; } = new();

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

    /// <summary>Phase 3 hot reload (SSE + eqc rebuild on save). Null = auto: ON in Development.</summary>
    public bool? HotReload { get; set; }

    public UIOptions WithSsr(bool enabled = true)
    {
        EnableSsr = enabled;
        return this;
    }

    /// <summary>
    /// The write-once design-system theme (an <see cref="eQuantic.UI.Primitives.IAppTheme"/>) this app
    /// renders with — <c>PhotonTheme.Instance</c>, <c>MaterialTheme.Instance</c>,
    /// <c>MaterialTheme.FromSeed(seed)</c>, or a brand theme. Null until set. When set, the SSR pipeline
    /// lowers the shared components with it, emits its token CSS, and bridges it to the client (boot
    /// calls <c>setPhotonTheme</c>) so hydration + client re-renders match the server.
    /// </summary>
    internal eQuantic.UI.Primitives.IAppTheme? Theme { get; private set; }

    /// <summary>The mode the app DECLARED, or null to follow the operating system (the default).</summary>
    internal eQuantic.UI.Primitives.ThemeMode? InitialThemeMode { get; private set; }

    /// <summary>
    /// Pin the app to one light/dark mode instead of following the operating system.
    /// <para>
    /// This drives <c>color-scheme</c>, which is the only thing every token actually reads: they
    /// resolve through <c>light-dark()</c>. Declaring a mode that did NOT reach
    /// <c>color-scheme</c> left two answers to one question — the page painted whatever the OS
    /// wanted while a toggle reported what the app had asked for, so a dark desktop got a dark
    /// page offering to "switch to dark".
    /// </para>
    /// <para>
    /// Leave it unset to follow the OS, which is what most apps want. Then the first paint is the
    /// OS's choice and a toggle's label is only settled once the browser's own controller resolves
    /// it at hydration — a server cannot know a preference it was never sent.
    /// </para>
    /// </summary>
    public UIOptions UseInitialThemeMode(eQuantic.UI.Primitives.ThemeMode mode)
    {
        InitialThemeMode = mode;
        return this;
    }

    /// <summary>Select the write-once theme for the whole app (SSR + client). See <see cref="Theme"/>.</summary>
    public UIOptions UseTheme(eQuantic.UI.Primitives.IAppTheme theme)
    {
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        return this;
    }

    /// <summary>
    /// Registers an asset provider for a specific component type.
    /// The provider will supply additional or overriding assets for the component.
    /// </summary>
    public UIOptions WithAssetProvider<TProvider>() where TProvider : class
    {
        var providerType = typeof(TProvider);
        foreach (var iface in providerType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IComponentAssetProvider<>))
            {
                AssetProviders.Add((iface, providerType));
                return this;
            }
        }

        throw new ArgumentException(
            $"{providerType.Name} does not implement IComponentAssetProvider<T>.",
            nameof(TProvider));
    }

    /// <summary>
    /// Registers a custom service registration callback.
    /// Used by packages to add services to the DI container during AddUI().
    /// </summary>
    public UIOptions RegisterServices(Action<IServiceCollection> registration)
    {
        ServiceRegistrations.Add(registration);
        return this;
    }

    /// <summary>
    /// Registers a custom endpoint configuration callback.
    /// Used by packages to map routes during MapUI().
    /// </summary>
    public UIOptions RegisterEndpoints(Action<IEndpointRouteBuilder> configuration)
    {
        EndpointConfigurations.Add(configuration);
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

        // Scan for Error Pages
        var pageTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<Core.PageAttribute>().Any());

        foreach (var type in pageTypes)
        {
            var attr = type.GetCustomAttributes<Core.PageAttribute>().First();
            RegisterErrorPage(type, attr.Route);
        }

        return this;
    }

    internal Type? NotFoundPageType { get; private set; }
    internal Type? ErrorPageType { get; private set; }

    internal void RegisterErrorPage(Type type, string route)
    {
        if (route == "/404") NotFoundPageType = type;
        if (route == "/500") ErrorPageType = type;
    }
}

/// <summary>
/// Options for generating the HTML shell.
/// </summary>
public class HtmlShellOptions
{
    public string Title { get; set; } = "eQuantic.UI App";
    public string HtmlClass { get; set; } = "";
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

    public HtmlShellOptions SetHtmlClass(string htmlClass)
    {
        HtmlClass = htmlClass;
        return this;
    }

    /// <summary>
    /// Adds a head element as DATA — <see cref="HtmlTag"/> owns the quoting, encoding and closing.
    /// This is the authoring API: app code never types markup.
    /// </summary>
    public HtmlShellOptions AddHeadTag(HtmlTag tag)
    {
        HeadTags.Add(tag.Render());
        return this;
    }

    /// <summary>
    /// ESCAPE HATCH: a head element as RAW markup, for anything the typed surface can't express
    /// yet. Prefer the <see cref="AddHeadTag(HtmlTag)"/> overload — nothing here is escaped.
    /// </summary>
    public HtmlShellOptions AddHeadTag(string rawHtml)
    {
        HeadTags.Add(rawHtml);
        return this;
    }

    /// <summary>The page's <c>&lt;meta name="description"&gt;</c>.</summary>
    public HtmlShellOptions AddDescription(string description) =>
        AddHeadTag(HtmlTag.Meta("description", description));

    /// <summary>A <c>&lt;link rel="preconnect"&gt;</c> to an origin the page will fetch from.
    /// <paramref name="crossOrigin"/> matters for font/CORS fetches (fonts.gstatic.com wants it).</summary>
    public HtmlShellOptions AddPreconnect(Uri origin, bool crossOrigin = false)
    {
        var tag = HtmlTag.Link("preconnect", origin.ToString());
        if (crossOrigin) tag.Attr("crossorigin");
        return AddHeadTag(tag);
    }

    /// <summary>An external stylesheet <c>&lt;link&gt;</c>.</summary>
    public HtmlShellOptions AddStylesheet(Uri href) =>
        AddHeadTag(HtmlTag.Link("stylesheet", href.ToString()));

    /// <summary>An external <c>&lt;script&gt;</c> (deferred by default — head scripts must not
    /// block parsing).</summary>
    public HtmlShellOptions AddScript(Uri src, bool defer = true) =>
        AddHeadTag(HtmlTag.Script(src.ToString(), defer));

    public HtmlShellOptions SetBaseStyles(string styles)
    {
        BaseStyles = styles;
        return this;
    }
}
