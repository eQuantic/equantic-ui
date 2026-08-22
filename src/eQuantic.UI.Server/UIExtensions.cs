using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

        // Provenance on every response — a startup filter, so no Program.cs has to remember a
        // middleware call and even the static bundles carry it. See PoweredByHeader for why the
        // value is the name alone.
        if (!options.DisablePoweredByHeader)
            services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, PoweredByHeader>();

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
        // The controller reads the request's cookie, so it needs the accessor.
        services.AddHttpContextAccessor();
        services.TryAddSingleton<eQuantic.UI.Primitives.IThemeController>(provider =>
            new SsrThemeController(
                provider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                options.InitialThemeMode));

        // The server half of the LANGUAGE hand (Track L D6). It reports the culture ASP.NET's own
        // middleware negotiated — a switcher rendering server-side must show the language the page
        // is actually in, and a component that resolves nothing has to guess.
        services.TryAddSingleton<eQuantic.UI.Primitives.ICultureController, SsrCultureController>();

        // What a device capability IS during server rendering: absent — see AbsentCapabilities.
        // A page that takes a camera or the app's storage has to be CONSTRUCTIBLE here, or the one
        // page that does something is the one page a crawler never sees. TryAdd throughout, so an
        // app with a genuine server-side answer for any of them registers it and wins.

        services.TryAddSingleton<eQuantic.UI.Primitives.IAppStorage, AbsentCapabilities.Storage>();
        services.TryAddSingleton<eQuantic.UI.Primitives.ISecretStore, AbsentCapabilities.Storage>();
        services.TryAddSingleton<eQuantic.UI.Primitives.ITextClipboard, AbsentCapabilities.Clipboard>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IPhotoLibrary, AbsentCapabilities.PhotoLibrary>();
        services.TryAddSingleton<eQuantic.UI.Primitives.ICamera, AbsentCapabilities.Camera>();
        services.TryAddSingleton<eQuantic.UI.Primitives.ILocation, AbsentCapabilities.Location>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IMotionSensor, AbsentCapabilities.MotionSensor>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IBiometrics, AbsentCapabilities.Biometrics>();
        services.TryAddSingleton<eQuantic.UI.Primitives.INetworkStatus, AbsentCapabilities.NetworkStatus>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IAnalytics, AbsentCapabilities.Analytics>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IClock, AbsentCapabilities.Clock>();

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
    /// <summary>
    /// Declares a page's route HERE, beside every other endpoint, instead of on the page itself:
    /// <c>app.MapPage&lt;HomePage&gt;("/")</c>.
    /// <para>
    /// The attribute stays, and for a page whose route is part of what it IS — a 404, a login — it
    /// remains the better answer. This is for the rest: routes an app wants to read in one place,
    /// routes that differ between hosts, a page mounted at a path its own file has no business
    /// knowing. It is also the only way to route a page from an assembly you do not own.
    /// </para>
    /// <para>
    /// Call it before the request that needs it — which in practice means where every other
    /// endpoint is declared, in Program.cs. The route registers in all three places a route has to
    /// exist: the endpoint table, the SSR page index, and the client's table for SPA navigation. A
    /// route that only half-registers is worse than none, because the page serves and then the
    /// first client-side link to it reloads the whole document for no visible reason.
    /// </para>
    /// </summary>
    /// <param name="route">The pattern, ASP.NET style: <c>/docs/{slug}</c>.</param>
    /// <param name="title">The document title, as <c>[Page(Title = …)]</c> would give it.</param>
    public static IEndpointRouteBuilder MapPage<TPage>(
        this IEndpointRouteBuilder endpoints, string route, string? title = null)
        where TPage : class
    {
        var pageType = typeof(TPage);
        if (!typeof(Core.IComponent).IsAssignableFrom(pageType)
            && !typeof(Primitives.UiComponent).IsAssignableFrom(pageType))
        {
            throw new ArgumentException(
                $"{pageType.Name} is not a page: it has to be a component (StatelessComponent, "
                + "StatefulComponent, or a write-once UiComponent).", nameof(TPage));
        }

        var options = endpoints.ServiceProvider.GetRequiredService<UIOptions>();
        options.DeclareRoute(route, pageType, title);

        // The same two endpoints a [Page] gets — the route, and its language-prefixed twin.
        foreach (var pattern in CultureEndpointPatterns(options, route))
            endpoints.MapGet(pattern, async context => await ServeAppShell(context, pageType.Name));
        return endpoints;
    }

    /// <summary>
    /// The endpoints one page route answers at: itself, and — when the app declared language
    /// prefixes — the same route behind <c>{culture:culture}</c>. ONE constrained pattern rather
    /// than a literal endpoint per language: the <c>culture</c> constraint admits exactly the
    /// prefixes the app named (anything else is a 404, which is the whole reason to name them),
    /// and the platform's <c>RouteDataRequestCultureProvider</c> reads the bound value. The home
    /// route is the case worth naming — <c>/</c> under <c>pt-BR</c> is <c>/pt-BR</c>, not
    /// <c>/pt-BR/</c>, which would be a second URL for one page.
    /// </summary>
    private static IEnumerable<string> CultureEndpointPatterns(UIOptions options, string route)
    {
        yield return route;
        if (options.CultureRoutes is null) yield break;
        yield return route == "/" ? $"/{{{CultureRouteConstraint.Name}:{CultureRouteConstraint.Name}}}"
                                  : $"/{{{CultureRouteConstraint.Name}:{CultureRouteConstraint.Name}}}{route}";
    }

    /// <summary>
    /// The same routes for the CLIENT's table, with the prefixes spelled out. The browser's matcher
    /// accepts a constraint it does not know, so <c>{culture:culture}</c> there would take any
    /// segment and claim <c>/fr/pricing</c> for a page the server refuses; the literal list keeps
    /// the two matchers answering the same.
    /// </summary>
    private static IEnumerable<string> CultureClientPatterns(UIOptions options, string route)
    {
        yield return route;
        if (options.CultureRoutes is not { } map) yield break;
        foreach (var prefix in map.Prefixed)
            yield return route == "/" ? $"/{prefix}" : $"/{prefix}{route}";
    }

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
                    // The bare route, plus the constrained one when the app declared languages.
                    foreach (var route in CultureEndpointPatterns(options, pageAttr.Route))
                    {
                        var name = pageType.Name;
                        endpoints.MapGet(route, async context => await ServeAppShell(context, name));
                    }
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

    /// <summary>
    /// The header a CLIENT NAVIGATION sends. The request goes to the target URL itself rather than
    /// to some side channel, which is the whole reason this works: route params, query values and
    /// the page's own resolution are the ones a full load would have, because it IS the same route.
    /// </summary>
    internal const string NavigationHeader = "X-EQ-Navigate";

    /// <summary>
    /// What a client navigation needs and could not get: the page's SERVER DATA and its METADATA.
    /// <para>
    /// A link used to swap the component and nothing else — no prefetch ran, so every navigated-to
    /// page rendered its empty state, and the head kept the previous document's title and canonical.
    /// Both are the same moment of the lifecycle: the things the server does around a page that the
    /// client never repeated.
    /// </para>
    /// <para>
    /// It runs the same code the shell runs, minus the DRAWING: prefetch, then metadata, from the
    /// request's own services, in one order and described in one place. Skipping the markup is what
    /// makes this cheap enough for the router to warm a page on hover — a tree build per hovered
    /// link would cost more than the round trip it saves.
    /// </para>
    /// </summary>
    private static async Task ServePageState(HttpContext context, string? pageName)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        // Never cached: this is the page's data, and the next visitor's is not this one's.
        context.Response.Headers["Cache-Control"] = "no-store";

        var options = context.RequestServices.GetRequiredService<UIOptions>();
        var rendering = pageName is null || !options.EnableSsr
            ? null
            : context.RequestServices.GetService<IServerRenderingService>();
        if (rendering is null)
        {
            await context.Response.WriteAsync("{}");
            return;
        }

        ServerRenderResult result;
        try
        {
            result = await rendering.PreparePageAsync(pageName!, context);
        }
        catch (Exception)
        {
            // A navigation must not be able to 500 the app: without the payload the page renders
            // its empty state, which is exactly where it was before this endpoint existed.
            await context.Response.WriteAsync("{}");
            return;
        }

        if (!result.Success)
        {
            await context.Response.WriteAsync("{}");
            return;
        }

        // The page's own answer travels too — a route that matched while its content did not exist
        // says so to a client navigation the same way it says it to a full load.
        if (result.StatusCode != StatusCodes.Status200OK)
            context.Response.StatusCode = result.StatusCode;

        var head = new StringBuilder();
        var title = options.HtmlShell.Title;
        if (result.Metadata is { } metadata)
        {
            if (!string.IsNullOrEmpty(metadata.Title)) title = metadata.Title;
            head.Append(metadata.RenderTags());
        }

        var payload = new StringBuilder("{");
        payload.Append("\"title\":").Append(JsonSerializer.Serialize(title));
        payload.Append(",\"head\":").Append(JsonSerializer.Serialize(head.ToString()));
        if (result.SerializedState is { Length: > 0 } state)
            payload.Append(",\"state\":").Append(state);
        payload.Append('}');
        await context.Response.WriteAsync(payload.ToString());
    }

    /// <summary>
    /// A bare URL asked for in a PREFIXED language goes to its prefixed address: the language the
    /// middleware negotiated for the request — the cookie the switcher writes, Accept-Language —
    /// becomes the URL, so what a reader sees, shares and bookmarks always names its language.
    /// 302, not 301: the preference is the reader's, and it changes. A URL that already carries a
    /// segment is left alone, and so is the default culture, which has none.
    /// </summary>
    private static bool RedirectToLanguageUrl(HttpContext context, UIOptions options)
    {
        if (options.CultureRoutes is not { } map) return false;
        if (context.Request.RouteValues.ContainsKey(CultureRouteConstraint.Name)) return false;
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (map.IsDefault(culture) || map.SegmentFor(culture).Length == 0) return false;

        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        context.Response.Redirect(map.PathFor(culture, path) + context.Request.QueryString, permanent: false);
        return true;
    }

    private static async Task ServeAppShell(HttpContext context, string? pageName)
    {
        // A client navigation asks the same route for the page's data instead of a document.
        if (context.Request.Headers.ContainsKey(NavigationHeader))
        {
            await ServePageState(context, pageName);
            return;
        }

        var options = context.RequestServices.GetRequiredService<UIOptions>();
        if (RedirectToLanguageUrl(context, options)) return;

        var shell = options.HtmlShell;
        // Per-request copy of the head tags. The HtmlShell is a singleton, so mutating
        // shell.HeadTags directly is not thread-safe and leaks tags across requests.
        var headTags = new List<string>(shell.HeadTags);
        var pageValue = pageName != null ? $"'{pageName}'" : "null";

        // Write-once theme selection (options.UseTheme): emit the selected theme's NORMATIVE token
        // stylesheet, and serialize it into window.__EQ_THEME__ so boot can setPhotonTheme before
        // hydration (SSR markup + client re-renders then resolve the same colors/shape). Absent a
        // selection, nothing is injected — the runtime keeps its baked-in photonTheme default.
        // The mode THIS request paints in: what the visitor last chose, or the app's declared
        // default when they have not chosen. The cookie is written by the browser's own controller
        // on a toggle (see WebThemeController) — no round trip, and the server can read it, which
        // localStorage would never let it do. This is what removes the flash: the very first byte
        // already carries the right mode, instead of the default arriving and hydration correcting
        // it in front of the reader.
        var requestedMode = ThemeCookie.Resolve(context, options.InitialThemeMode, options.ThemeCookie?.Name);

        string? themeDataJson = null;
        if (options.Theme is { } appTheme)
        {
            headTags.Add($"<style>{eQuantic.UI.Web.PhotonCssGenerator.Generate(appTheme, requestedMode)}</style>");
            themeDataJson = eQuantic.UI.Web.ThemeBridge.SerializeJson(appTheme);
        }

        // Track L D4/D5: the request's culture rides the shell — <html lang> for assistive tech
        // (pronunciation follows it), and window.__EQ_CULTURE__ with the ACTIVE catalog inlined so
        // the client resolves exactly the strings the server rendered, before hydration. The
        // culture itself is ASP.NET's answer (RequestLocalization when the app wired it, the
        // process default otherwise) — the SDK only reads the statics, per the plan.
        var uiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        var formatCulture = System.Globalization.CultureInfo.CurrentCulture;
        var htmlLang = uiCulture.Name.Length > 0 ? uiCulture.Name : "en";
        var cultureDataJson = CultureBridge.BuildCultureData(context, uiCulture, formatCulture);

        // The app icon the SDK generated, linked without the app saying so. Stating it once — in
        // Assets/ — and having it appear in the tab, on a pinned home screen and in the install
        // manifest is the whole point; asking an author to also write four <link> tags would be
        // handing back the work we just took.
        headTags.AddRange(GeneratedIconTags(context));

        // Initialize Metadata
        var metadata = new MetadataCollection { Title = shell.Title };
        // The app's defaults FIRST, so the page's own (merged below) overrides them by key instead
        // of landing beside them as a duplicate tag.
        foreach (var tag in shell.DefaultMetadata.Tags) metadata.AddOrUpdate(tag);
        var seo = new SeoBuilder(metadata);

        // The translation group, BEFORE the page speaks: an app-wide policy is a default, and a
        // page with something better to say (a slug that is not a translation of this one) writes
        // its own Alternate and wins by key.
        AddAlternateLinks(context, options, seo);

        // Attempt SSR if page name is provided and SSR is enabled
        var ssrContent = "<div class=\"loading\">Loading...</div>";
        // The page's paint servers, if it drew any — see GradientSink for why they are not inside
        // each drawing any more.
        string? vectorDefs = null;
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
                        vectorDefs = result.VectorDefs;
                        ssrEnabled = true;
                        serializedState = result.SerializedState;

                        // What the PAGE asked to answer with (IHandleStatus). A route that matched
                        // while its content did not exist renders the right thing for a reader and
                        // must not tell every machine the request succeeded — a crawler would index
                        // the empty page and a link checker would call the site healthy.
                        if (result.StatusCode != StatusCodes.Status200OK)
                            context.Response.StatusCode = result.StatusCode;

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
                        vectorDefs = result.VectorDefs;
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
                        vectorDefs = result.VectorDefs;
                             ssrEnabled = true; // Enabled for the error page
                             if (!string.IsNullOrEmpty(result.Metadata?.Title)) metadata.Title = result.Metadata.Title;
                         }
                    }
                }
                catch { /* Fallback to client-side error */ }
            }
        }

        // The canonical LAST, after the page has had its say. A page writes
        // seo.Canonical("https://site/products") once, and under a language prefix that same
        // string tells a search engine to index the ENGLISH page instead of this one — the exact
        // opposite of what putting the language in the URL was for, and silent: the markup looks
        // right on every page.
        LocalizeCanonical(context, options, metadata);

        // Client route table from [Page] attributes — lets the runtime resolve URLs to page bundles
        // for client-side (SPA) navigation without a server round-trip.
        static string JsStr(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");
        var routeEntries = options.AssembliesToScan
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetCustomAttributes<Core.PageAttribute>()
                .Select(attr => (Pattern: attr.Route, Page: t.Name, attr.Title)))
            .Concat(options.DeclaredRoutes.Select(r => (Pattern: r.Pattern, Page: r.Page.Name, r.Title)))
            // The prefixed URLs go in the table too, or the FIRST client-side navigation inside a
            // translated page finds no match and falls back to a full reload — the language would
            // survive and the SPA would not, which is the kind of regression nobody reports.
            .SelectMany(r => CultureClientPatterns(options, r.Pattern)
                .Select(pattern => (Pattern: pattern, r.Page, r.Title)))
            .Distinct()
            .ToList();
        var routesJson = "[" + string.Join(",", routeEntries.Select(r =>
        {
            var title = string.IsNullOrEmpty(r.Title) ? "" : $",title:'{JsStr(r.Title)}'";
            return $"{{pattern:'{JsStr(r.Pattern)}',page:'{JsStr(r.Page)}'{title}}}";
        })) + "]";

        // Inject configuration object
        // The cookie config crosses to the browser because the browser is what WRITES it while the
        // server READS it. Two places to configure would drift, and a drifted name fails silently:
        // the server reads a cookie nobody writes, so persistence stops while everything still
        // looks right. `false` is the app having turned it off.
        var themeCookieJson = options.ThemeCookie is { } cookie
            ? $"{{ name: '{cookie.Name}', days: {cookie.Days} }}"
            : "false";

        // The language-prefix policy crosses because the browser has to APPLY it: an href lowered
        // after hydration, and the switcher's own navigation, both need to know which segments are
        // languages. Guessing by shape would call a page named `pt` a language.
        var cultureRoutesJson = options.CultureRoutes is { } cultureMap
            ? $"{{ default: '{JsStr(cultureMap.Default)}', prefixed: ["
              + string.Join(",", cultureMap.Prefixed.Select(p => $"'{JsStr(p)}'")) + "] }"
            : "null";

        var configJson = $@"{{
            page: {pageValue},
            version: '{BuildId}',
            ssr: {ssrEnabled.ToString().ToLowerInvariant()},
            themeCookie: {themeCookieJson},
            cultureRoutes: {cultureRoutesJson},
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
               .Set("HtmlLang", htmlLang)
               .Set("Title", System.Web.HttpUtility.HtmlEncode(metadata.Title))
               .Set("MetadataTags", metadata.RenderTags())
               .Set("BuildId", BuildId)
               .SetOrEmpty("BaseStyles", shell.BaseStyles)
               .Set("HeadTags", string.Join("\n    ", headTags))
               .Set("SsrEnabled", ssrEnabled.ToString().ToLowerInvariant())
               .Set("SsrContent", ssrContent)
               .SetOrEmpty("VectorDefs", vectorDefs)
               .Set("ConfigJson", configJson)
               .Set("IsDevelopmentBool", isDevelopment ? "true" : "false")
               .SetOrEmpty("InitialState", serializedState != null ? $"window.__INITIAL_STATE__ = {serializedState};" : null)
               .SetOrEmpty("ThemeData", themeDataJson != null ? $"window.__EQ_THEME__ = {themeDataJson};" : null)
               .SetOrEmpty("CultureData", cultureDataJson != null ? $"window.__EQ_CULTURE__ = {cultureDataJson};" : null);

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
    /// <summary>
    /// The page's <c>rel="alternate" hreflang</c> set, from the app's URL policy and the languages
    /// it advertises.
    /// <para>
    /// Three rules the standard imposes, all of them enforced here rather than left to the app:
    /// the set includes the page ITSELF (a group that omits the current page is discarded whole),
    /// every URL is ABSOLUTE (a relative hreflang is dropped silently), and <c>x-default</c> names
    /// the default culture's URL, which is where a visitor whose language matched nothing lands.
    /// </para>
    /// <para>
    /// Nothing is emitted when the app declared no policy, or when it has a single culture: a
    /// translation group of one says the page exists in no other language, which is a claim, not
    /// an absence.
    /// </para>
    /// </summary>
    /// <summary>
    /// Puts the request's language on the canonical URL, when the app serves languages as a path
    /// prefix. Idempotent — a canonical that already names a language has it replaced, not stacked
    /// — and it only ever rewrites the PATH, so the scheme and host a page chose stay its own.
    /// </summary>
    private static void LocalizeCanonical(HttpContext context, UIOptions options, MetadataCollection metadata)
    {
        if (options.CultureRoutes is not { } map) return;
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;

        foreach (var tag in metadata.Tags.OfType<LinkTag>().Where(t => t.Rel == "canonical").ToList())
        {
            var localized = LocalizeUrlPath(map, culture, tag.Href);
            if (localized == tag.Href) continue;
            metadata.AddOrUpdate(new LinkTag("canonical", localized));
            metadata.AddOrUpdate(new PropertyMetaTag("og:url", localized));
        }
    }

    /// <summary>The same URL with the language on its path. Absolute or rooted — an absolute one
    /// keeps everything but the path.</summary>
    private static string LocalizeUrlPath(CultureRouteMap map, string culture, string url)
    {
        if (url.StartsWith('/')) return map.PathFor(culture, url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute)) return url;
        if (absolute.Scheme is not ("http" or "https")) return url;
        var builder = new UriBuilder(absolute) { Path = map.PathFor(culture, absolute.AbsolutePath) };
        return builder.Uri.ToString();
    }

    private static void AddAlternateLinks(HttpContext context, UIOptions options, SeoBuilder seo)
    {
        // Declared language prefixes ARE an alternate-URL policy — the map answers where every page
        // lives in every language — so an app that asked for them gets the hreflang group for
        // free, and its own policy still wins when it wrote one.
        var policy = options.AlternateUrl
            ?? (options.CultureRoutes is { } map
                ? request => map.PathFor(request.Culture, request.Request.Path.HasValue ? request.Request.Path.Value! : "/")
                : (Func<AlternateRequest, string?>?)null);
        if (policy is null) return;

        var localization = context.RequestServices
            .GetService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>()?.Value;
        // The app's own list first, the container's second: `app.UseRequestLocalization(o => …)`
        // builds its options INLINE and registers nothing, so the container answers with the
        // invariant default — a single culture, and a head that comes out empty without saying why.
        var cultures = (options.AlternateCultures
                ?? options.CultureRoutes?.All.ToList()
                ?? localization?.SupportedUICultures?.Select(culture => culture.Name).ToList()
                ?? [])
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cultures.Count < 2) return;

        foreach (var culture in cultures)
        {
            if (policy(new AlternateRequest(culture, context.Request)) is not { Length: > 0 } url) continue;
            seo.Alternate(culture, AlternateUrls.Absolute(context.Request, url));
        }

        // x-default follows the app's OWN default when the middleware shared one — the same question
        // it already answered for a request that matched nothing — and otherwise the FIRST language
        // the app named, because the order it writes them in is the order it means them.
        var fallback = localization?.DefaultRequestCulture.UICulture.Name is { Length: > 0 } named
            && cultures.Contains(named, StringComparer.OrdinalIgnoreCase)
                ? named
                : cultures[0];
        if (policy(new AlternateRequest(fallback, context.Request)) is { Length: > 0 } defaultUrl)
            seo.AlternateDefault(AlternateUrls.Absolute(context.Request, defaultUrl));
    }

}

/// <summary>
/// Configuration options for UI services.
/// </summary>
/// <summary>How the theme is remembered — see <see cref="UIOptions.UseThemeCookie"/>.</summary>
public sealed class ThemeCookieOptions
{
    public string Name { get; init; } = "eq-theme";
    public int Days { get; init; } = 365;
}

/// <summary>The cookie the browser's theme controller writes, and the only reason the server can
/// paint a visitor's chosen mode on the first byte.</summary>
public static class ThemeCookie
{
    public const string Name = "eq-theme";

    /// <summary>
    /// The mode this request should paint in: the visitor's remembered choice, or
    /// <paramref name="declared"/> when they have not made one (null there means "follow the OS",
    /// which the server cannot resolve and must leave to CSS).
    /// <para>
    /// An unrecognised cookie value is ignored rather than trusted — it is user-supplied text, and
    /// the only two answers this can have are light and dark.
    /// </para>
    /// </summary>
    public static eQuantic.UI.Primitives.ThemeMode? Resolve(
        HttpContext? context, eQuantic.UI.Primitives.ThemeMode? declared, string? cookieName = null) =>
        context?.Request.Cookies[cookieName ?? Name] switch
        {
            "dark" => eQuantic.UI.Primitives.ThemeMode.Dark,
            "light" => eQuantic.UI.Primitives.ThemeMode.Light,
            _ => declared,
        };
}


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

    /// <summary>The cookie the theme is remembered in, or null when the app has turned it off.</summary>
    internal ThemeCookieOptions? ThemeCookie { get; private set; } = new();

    /// <summary>
    /// Name and lifetime of the cookie the visitor's theme choice is remembered in.
    /// <para>
    /// It is ONE setting because both halves must agree: the browser's controller writes this
    /// cookie and the server reads it. Configure them separately and they drift, at which point the
    /// server reads a name nobody writes — persistence stops working while every part of it still
    /// looks correct.
    /// </para>
    /// <para>
    /// Worth naming when two eQuantic apps share a domain and should not inherit each other's
    /// theme, or when an existing site already has a cookie convention.
    /// </para>
    /// </summary>
    public UIOptions UseThemeCookie(string name = "eq-theme", int days = 365)
    {
        ThemeCookie = new ThemeCookieOptions { Name = name, Days = days };
        return this;
    }

    /// <summary>
    /// Do not remember the theme at all — no cookie is written, and every visit starts from
    /// <see cref="UseInitialThemeMode"/> or the operating system.
    /// <para>
    /// For a site that must not set a cookie before consent. It is a build-time switch, so an app
    /// that wants to start persisting the moment a visitor accepts a banner needs its own hand on
    /// the writing rather than this.
    /// </para>
    /// </summary>
    public UIOptions WithoutThemeCookie()
    {
        ThemeCookie = null;
        return this;
    }

    /// <summary>
    /// How this page is reached in each of the app's other languages. Set it and every rendered
    /// page carries its own <c>rel="alternate" hreflang</c> set — one link per supported culture,
    /// the page ITSELF among them, plus <c>x-default</c> — instead of each page spelling the group
    /// out by hand and drifting the day a language is added.
    /// <para>
    /// The cultures come from the app's own <c>RequestLocalization</c>, because that is where a
    /// .NET app already declares them; the SDK ships no second list to keep in step. An app that
    /// never wired localization has one language, and one language has no alternates.
    /// </para>
    /// <para>
    /// The delegate answers a URL, absolute or rooted, or NULL when the page has no version in
    /// that language — a group that promises a translation which 404s is worse than a smaller
    /// group. <see cref="AlternateUrls"/> has the two common shapes ready.
    /// </para>
    /// <para>
    /// NAME THE LANGUAGES unless the app shares its localization options through DI. The middleware
    /// overload every sample uses — <c>app.UseRequestLocalization(o => …)</c> — builds its options
    /// INLINE and registers nothing, so asking the container answers with the invariant default and
    /// the head comes out empty. An app that calls
    /// <c>services.Configure&lt;RequestLocalizationOptions&gt;(…)</c> and then
    /// <c>app.UseRequestLocalization()</c> can leave this out and keep one list.
    /// </para>
    /// <example>
    /// <code>
    /// options.UseAlternateLinks(AlternateUrls.PathPrefix(), "en", "pt-BR", "es");
    /// options.UseAlternateLinks(r => r.Culture == "pt-BR" ? $"/pt{r.Request.Path}" : r.Request.Path);
    /// </code>
    /// </example>
    /// </summary>
    public UIOptions UseAlternateLinks(Func<AlternateRequest, string?> url, params string[] cultures)
    {
        AlternateUrl = url;
        AlternateCultures = cultures.Length > 0 ? cultures : null;
        return this;
    }

    /// <summary>
    /// Serve every page under a LANGUAGE PREFIX — <c>/pt-BR/pricing</c> — with the first culture
    /// named here staying unprefixed.
    /// <para>
    /// One declaration reaches the three places that have to agree: the endpoints each page is
    /// mapped at, the client route table the runtime navigates with, and the href every in-app
    /// link carries. A prefix the app never named matches no endpoint, so it 404s rather than
    /// serving one language under another language's URL.
    /// </para>
    /// <para>
    /// Negotiation stays ASP.NET's: this configures the platform's own
    /// <c>RequestLocalizationOptions</c> from the list (default, supported cultures, and its
    /// <c>RouteDataRequestCultureProvider</c> first, reading the <c>{culture:culture}</c> segment)
    /// and teaches the route constraint map the word <c>culture</c>. The app adds
    /// <c>app.UseRequestLocalization()</c> and is done. An app that prefers the middleware's lambda
    /// overload — which builds its options inline and sees nothing from DI — passes the same list
    /// there with <see cref="CultureRouteExtensions.UseCultureRoutes(RequestLocalizationOptions, string[])"/>.
    /// </para>
    /// <para>
    /// A bare URL asked for in a prefixed language (a cookie the switcher wrote, an Accept-Language)
    /// is redirected to its prefixed address, so what a reader sees, shares and bookmarks always
    /// names its language; and the <c>hreflang</c> group is emitted from the same map unless the
    /// app declared its own policy with <see cref="UseAlternateLinks"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddUI(o => o.UseCultureRoutes("en", "pt-BR", "es"));
    /// …
    /// app.UseRequestLocalization();
    /// app.MapUI();
    /// </code>
    /// </example>
    public UIOptions UseCultureRoutes(params string[] cultures)
    {
        var map = CultureRouteMap.From(cultures);
        CultureRoutes = map;
        ServiceRegistrations.Add(services => services.AddCultureRoutes(map));
        return this;
    }

    /// <summary>The language-prefix policy, or null when the app never asked for one.</summary>
    public CultureRouteMap? CultureRoutes { get; private set; }

    /// <summary>The alternate-URL policy, or null when the app never set one. See
    /// <see cref="UseAlternateLinks"/>.</summary>
    public Func<AlternateRequest, string?>? AlternateUrl { get; private set; }

    /// <summary>The languages the group advertises, when the app named them here. Null means
    /// "ask the app's RequestLocalization" — see <see cref="UseAlternateLinks"/>.</summary>
    public IReadOnlyList<string>? AlternateCultures { get; private set; }

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

    /// <summary>
    /// Routes the app declared in <c>Program.cs</c> with <c>MapPage&lt;T&gt;</c>, rather than on the
    /// page with <c>[Page]</c>.
    /// <para>
    /// Both are read everywhere a route is read — the endpoint table, the client's route table for
    /// SPA navigation, and the SSR page index — because a route that only half-registers is worse
    /// than one that does not: the page serves, and then the first client-side link to it reloads
    /// the whole document for no visible reason.
    /// </para>
    /// </summary>
    public IReadOnlyList<(string Pattern, Type Page, string? Title)> DeclaredRoutes => _declaredRoutes;

    private readonly List<(string Pattern, Type Page, string? Title)> _declaredRoutes = new();

    /// <summary>Records a route declared in Program.cs. Called by <c>MapPage&lt;T&gt;</c>.</summary>
    internal void DeclareRoute(string pattern, Type page, string? title)
    {
        _declaredRoutes.Add((pattern, page, title));
    }

    public UIOptions ConfigureHtmlShell(Action<HtmlShellOptions> configure)
    {
        configure(HtmlShell);
        return this;
    }

    /// <summary>
    /// Turns off the <c>x-powered-by: eQuantic.UI</c> response header. It is ON by default —
    /// provenance, name only, no version — and this exists for the app whose hardening checklist
    /// flags any x-powered-by at all.
    /// </summary>
    public bool DisablePoweredByHeader { get; set; }

    /// <summary>Fluent spelling of <see cref="DisablePoweredByHeader"/>.</summary>
    public UIOptions WithoutPoweredByHeader()
    {
        DisablePoweredByHeader = true;
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

    /// <summary>
    /// App-wide metadata DEFAULTS — a description, a fallback share image, a Twitter card type:
    /// whatever every page should say unless it says otherwise.
    /// <para>
    /// These seed the same collection a page's <c>IHandleMetadata</c> writes into, so a page
    /// overrides them BY KEY rather than adding a second tag. That is the whole point: these used
    /// to be raw head HTML, which shares no key with anything, so an app that set a description
    /// here and a page that set its own shipped two <c>&lt;meta name="description"&gt;</c> — a
    /// crawler being told the page cannot make up its mind — and there was no way for a page to
    /// win. The only way out was to leave the global empty, which made it useless for exactly the
    /// things a global is for.
    /// </para>
    /// </summary>
    public HtmlShellOptions ConfigureMetadata(Action<Core.Metadata.SeoBuilder> configure)
    {
        configure(new Core.Metadata.SeoBuilder(DefaultMetadata));
        return this;
    }

    /// <summary>
    /// The metadata every page starts from; a page's own overrides it BY KEY. Public because it is
    /// what the merge reads and what a test has to be able to assert on — the seeding order is the
    /// whole contract, and a contract nothing can observe is one nothing can hold you to.
    /// </summary>
    public Core.Metadata.MetadataCollection DefaultMetadata { get; } = new();

    /// <summary>The default <c>&lt;meta name="description"&gt;</c> — a page's own replaces it.</summary>
    public HtmlShellOptions AddDescription(string description) =>
        ConfigureMetadata(seo => seo.Description(description));

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
