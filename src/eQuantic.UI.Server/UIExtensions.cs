using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Server.Assets;
using eQuantic.UI.Server.Client;
using eQuantic.UI.Server.Metadata;
using eQuantic.UI.Server.Authorization;
using eQuantic.UI.Server.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        // The faces the app serves (#1055, #1056), one set the render and the font routes share: read at
        // the first request from the theme the app chose and the catalogs of the assemblies it scans, and
        // added to as pages name other families.
        services.TryAddSingleton(provider => ServedFonts.Of(options.Theme ?? PhotonTheme.Instance, options.AssembliesToScan,
            provider.GetService<ILoggerFactory>()?.CreateLogger("eQuantic.UI.Fonts")));
        // The origins whose pages may call the app's actions besides its own host (#678): read from
        // configuration, added to in Program.cs, and checked at start, so a malformed one stops the app
        // instead of refusing the page it was written for.
        services.AddOptions<ServerActionsOptions>()
            .BindConfiguration(ServerActionsOptions.SectionName)
            .Configure(actions =>
            {
                foreach (var origin in options.ServerActionOrigins) actions.AllowedOrigins.Add(origin);
            })
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ServerActionsOptions>, ServerActionsOptionsValidator>());
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
        // A refused CLIENT NAVIGATION into a page that requires authorization is answered 401 or 403,
        // which the router acts on, instead of a challenge a fetch cannot follow (#673).
        // It WRAPS the handler the app registered before AddUI, so the app's own answers stand for
        // every request that is not a navigation; the framework's when there is none.
        NavigationAuthorizationResultHandler.Decorate(services);

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
        // Consent is read from the request's eq-consent cookie, so the server paints the same
        // answer the browser holds — see SsrConsent for why that is knowledge, not simulation.
        services.TryAddSingleton<eQuantic.UI.Primitives.IConsent, SsrConsent>();
        // The request's thread is the UI thread here: SetState runs inline, posted work runs now.
        services.TryAddSingleton<eQuantic.UI.Primitives.IUiDispatcher, SsrUiDispatcher>();

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
        services.TryAddSingleton<eQuantic.UI.Primitives.IPasskeys, AbsentCapabilities.Passkeys>();
        services.TryAddSingleton<eQuantic.UI.Primitives.INetworkStatus, AbsentCapabilities.NetworkStatus>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IServerEvents, AbsentCapabilities.ServerEvents>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IAnalytics, AbsentCapabilities.Analytics>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IClock, AbsentCapabilities.Clock>();
        services.TryAddSingleton<eQuantic.UI.Primitives.IFrameTicker, AbsentCapabilities.FrameTicker>();

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
    /// <param name="endpoints">The endpoint builder this page is registered on.</param>
    /// <param name="route">The pattern, ASP.NET style: <c>/docs/{slug}</c>.</param>
    /// <param name="title">The document title, as <c>[Page(Title = …)]</c> would give it.</param>
    public static IEndpointRouteBuilder MapPage<TPage>(
        this IEndpointRouteBuilder endpoints, string route, string? title = null)
        where TPage : class
    {
        var pageType = typeof(TPage);
        if (!typeof(IComponent).IsAssignableFrom(pageType)
            && !typeof(Primitives.UiComponent).IsAssignableFrom(pageType))
        {
            throw new ArgumentException(
                $"{pageType.Name} is not a page: it has to be a component (StatelessComponent, "
                + "StatefulComponent, or a write-once UiComponent).", nameof(TPage));
        }

        // A page declared inside a [ServerOnly] or a [RuntimeProvided] class, or an attribute, has no
        // module the browser can load, as the build writes none for its owner (#584): refused here,
        // where a [Page] of the same shape is left out of the route table, rather than served and
        // broken. The message names every owner TwinName.OwnersCross refuses: it listed the ones that
        // never reach the browser, and a [RuntimeProvided] one does, inside the runtime. An exception
        // class of the app's is no such owner: it is a class, with a module of its own (#611).
        if (!TwinName.OwnersCross(pageType))
        {
            throw new ArgumentException(
                $"{pageType.FullName} is declared inside a type the build writes no module for ([ServerOnly], "
                + "[RuntimeProvided] or an attribute), so it has none either: declare the page outside it.",
                nameof(TPage));
        }

        var options = endpoints.ServiceProvider.GetRequiredService<UIOptions>();
        options.DeclareRoute(route, pageType, title);

        // The same two endpoints a [Page] gets — the route, and its language-prefixed twin.
        // The page's own [Authorize] travels with it, wherever it is routed from (#673).
        foreach (var pattern in CultureEndpointPatterns(options, route))
            endpoints.MapGetAndHead(pattern, async context => await ServeAppShell(context, pageType, new DeclaredPage(title, null)))
                .WithPageAuthorization(pageType);
        return endpoints;
    }

    /// <summary>
    /// GET and HEAD at <paramref name="pattern"/>. HTTP defines HEAD as GET without the content (RFC
    /// 9110, section 9.3.2), and an uptime monitor, a link checker or a crawler asks with it: mapped
    /// for GET alone, a HEAD fell through to the fallback and every page answered 404 (#575). The
    /// handler is the GET's, and the server writes no body for a HEAD.
    /// </summary>
    private static IEndpointConventionBuilder MapGetAndHead(this IEndpointRouteBuilder endpoints, string pattern, RequestDelegate handler) =>
        endpoints.MapMethods(pattern, [HttpMethods.Get, HttpMethods.Head], handler);

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
                .Where(t => t.GetCustomAttributes<PageAttribute>().Any() && TwinName.OwnersCross(t));

            foreach (var pageType in pageTypes)
            {
                var pageAttrs = pageType.GetCustomAttributes<PageAttribute>();
                
                foreach (var pageAttr in pageAttrs) 
                {
                    // The bare route, plus the constrained one when the app declared languages.
                    var declared = new DeclaredPage(pageAttr.Title, pageAttr.Description);
                    // The endpoint carries the page's TYPE: by its simple name, two pages of one name in
                    // two namespaces rendered as one (#514).
                    // And its [Authorize], as the endpoint's metadata: the app's authorization
                    // middleware refuses a request before the page is built (#673).
                    foreach (var route in CultureEndpointPatterns(options, pageAttr.Route))
                        endpoints.MapGetAndHead(route, async context => await ServeAppShell(context, pageType, declared))
                            .WithPageAuthorization(pageType);
                }
            }
        }

        // A culture switch's format half (#471): what the culture a page switches to with no reload
        // formats with and what its calendar is called, written from .NET as the shell writes them
        // for a request in that culture, so a number reads after the switch as the server writes it
        // on the next request. A name that is no culture .NET knows is not found.
        endpoints.MapGetAndHead("/_equantic/culture/{name}.json", async context =>
        {
            var name = (string?)context.GetRouteValue("name");
            var document = string.IsNullOrEmpty(name) ? null : CultureBridge.FormatDocument(name);
            if (document is null)
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "application/json";
            context.Response.Headers["Cache-Control"] = "no-cache";
            await context.Response.WriteAsync(document);
        });

        // The faces the app ships (#1055), by the hash of their bytes: immutable, and only what the
        // theme, the app's catalogs and its pages named, since a name is the only way in. Public as the
        // runtime is: a sign-in page under a fallback policy is set in them too.
        endpoints.MapGetAndHead("/_equantic/fonts/{name}", async context =>
        {
            var font = context.RequestServices.GetRequiredService<ServedFonts>().Find((string?)context.GetRouteValue("name") ?? "");
            if (font is null)
            {
                context.Response.StatusCode = 404;
                return;
            }
            // Compressed once when the set was read, and sent so to a browser that takes Brotli, which
            // every current one does: the app's own response compression is not relied on, since no
            // middleware of it runs unless the app adds one.
            var brotli = context.Request.GetTypedHeaders().AcceptEncoding
                .Any(encoding => encoding.Value.Equals("br", StringComparison.OrdinalIgnoreCase) && encoding.Quality is not 0);
            var body = brotli ? font.Brotli : font.Bytes;
            context.Response.ContentType = font.ContentType;
            if (brotli) context.Response.Headers.ContentEncoding = "br";
            context.Response.Headers.Vary = "Accept-Encoding";
            context.Response.ContentLength = body.Length;
            context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            await context.Response.Body.WriteAsync(body);
        }).AllowAnonymous();

        // A face a page the browser builds names (#1056), asked for by its identity, an assembly and a
        // resource, since the browser does not know the bytes' hash: sent on to the content-addressed URL,
        // so every page shares one cached copy of a file. Only a face an eQuantic build embedded, in an
        // assembly the app has loaded (ServedFonts.Locate). The redirect itself is asked again every
        // time, so a new build's file is found under its new hash.
        endpoints.MapGetAndHead("/_equantic/fonts/{assembly}/{**resource}", context =>
        {
            var font = context.RequestServices.GetRequiredService<ServedFonts>().Locate(
                (string?)context.GetRouteValue("assembly") ?? "", (string?)context.GetRouteValue("resource") ?? "");
            if (font is null)
            {
                context.Response.StatusCode = 404;
                return Task.CompletedTask;
            }
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Redirect(font.Url);
            return Task.CompletedTask;
        }).AllowAnonymous();

        // Map Runtime JS (immutable via BuildId in URL, long cache)
        endpoints.MapGetAndHead("/_equantic/runtime.js", async context =>
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
        })
        // The SDK's own code is public: a sign-in page under an app's fallback policy has to load it
        // to come alive (#673). What a page may show is the page's route's to decide.
        .AllowAnonymous();

        // Debug/Fallback: Manually serve component files if StaticFiles misses them
        endpoints.MapGetAndHead("/_equantic/{name}.js", async context =>
        {
            var name = (string?)context.GetRouteValue("name");
            var path = AssetPaths.Resolve(
                Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "_equantic"),
                name, ".js");

            if (path is not null && File.Exists(path))
            {
                context.Response.ContentType = "application/javascript";
                // Hot reload rewrites fixed-name bundles in place, and the build id in their URL is
                // the process's — immutable caching would pin the browser to the pre-edit code
                // forever. A rebuilding app revalidates; any other stays immutable.
                var hotReloads = context.RequestServices.GetRequiredService<UIOptions>()
                    .HotReloads(context.RequestServices.GetRequiredService<IWebHostEnvironment>());
                context.Response.Headers["Cache-Control"] = hotReloads
                    ? "no-cache"
                    : "public, max-age=31536000, immutable";
                await context.Response.SendFileAsync(path);
            }
            else
            {
                context.Response.StatusCode = 404;
                // Try finding it in the local directory (Dev scenario)
                var localPath = AssetPaths.Resolve(
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "_equantic"), name, ".js");
                if (localPath is not null && File.Exists(localPath))
                {
                    context.Response.ContentType = "application/javascript";
                    await context.Response.SendFileAsync(localPath);
                }
                else if (path is not null)
                {
                    // The paths are the server's own, and this route serves anyone: they are for a
                    // developer, so only Development writes them.
                    context.Response.ContentType = "application/javascript";
                    await context.Response.WriteAsync(
                        context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()
                            ? $"// 404: Component {name} not found at {path} or {localPath}"
                            : $"// 404: Component {name} not found");
                }
            }
        }).AllowAnonymous();

        // Debug/Fallback: serve component source maps so C# breakpoints bind — including on pages reached
        // via client-side (SPA) navigation, where the page bundle is dynamically imported. Without this,
        // the `.js` loads but its `.js.map` 404s and the debugger can't map back to C#. Not cached
        // immutably (the map URL carries no version query, so a stale map must be revalidated).
        endpoints.MapGetAndHead("/_equantic/{name}.js.map", async context =>
        {
            var name = (string?)context.GetRouteValue("name");
            var webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var candidates = new[]
            {
                AssetPaths.Resolve(Path.Combine(webRoot, "_equantic"), name, ".js.map"),
                AssetPaths.Resolve(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "_equantic"), name, ".js.map"),
            };
            var mapPath = candidates.FirstOrDefault(candidate => candidate is not null && File.Exists(candidate));

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
        }).AllowAnonymous();

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

        // Phase 3 hot reload — in Development and under dotnet watch, unless the app says: watch
        // sources, re-run the SDK's eqc target, notify browsers over SSE (the runtime replays live
        // state after the reload).
        var environment = endpoints.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        if (options.HotReloads(environment))
        {
            var hotReload = new HotReload.HotReloadService(environment.ContentRootPath);
            hotReload.Start();
            // Its lifetime is the app's. dotnet watch stops an app it restarts with SIGTERM to that
            // process alone, and a rebuild still running would go on writing the files the restart's
            // own build writes.
            endpoints.ServiceProvider.GetRequiredService<IHostApplicationLifetime>()
                .ApplicationStopping.Register(hotReload.Dispose);
            endpoints.MapGet("/_equantic/hmr", hotReload.HandleClient).AllowAnonymous();
        }

        if (environment.IsDevelopment())
        {
            // The stage-one source maps (TS intermediate → C#, C# text embedded) for the error
            // overlay's second hop, mapped where the overlay installs, in Development alone: the
            // maps carry the app's C#, and a run under dotnet watch in another environment streams
            // rebuilds without serving its source. Name-only — no separators survive the check, so
            // nothing above obj/eQuantic/ts is reachable.
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
            }).AllowAnonymous();
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
            await ServeAppShell(context, null, null);
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
    /// What a route's document says about its page before the page speaks, the same for a full load
    /// and a client navigation: the app's defaults, then what the route declares, then its translation
    /// group, each winning over the one before by key. What the page says itself
    /// (<see cref="AdoptPageMetadata"/>) goes on top.
    /// <para>
    /// The route's title used to be applied after the page, and only when the metadata had none, which
    /// the app's default title always filled, so no page ever got it; and a navigation never looked at
    /// it, answering with the app's title over the one the client had just set from its route table.
    /// </para>
    /// </summary>
    private static MetadataCollection PageMetadata(HttpContext context, UIOptions options, DeclaredPage? declared)
    {
        var shell = options.HtmlShell;
        var metadata = new MetadataCollection { Title = shell.Title };
        foreach (var tag in shell.DefaultMetadata.Tags) metadata.AddOrUpdate(tag);
        var seo = new SeoBuilder(metadata);
        if (!string.IsNullOrEmpty(declared?.Title)) seo.Title(declared.Title);
        if (!string.IsNullOrEmpty(declared?.Description)) seo.Description(declared.Description);
        // The translation group, BEFORE the page speaks: an app-wide policy is a default, and a page
        // with something better to say (a slug that is not a translation of this one) writes its own
        // Alternate and wins by key.
        AddAlternateLinks(context, options, seo);
        return metadata;
    }

    /// <summary>What the page says of itself (<c>IHandleMetadata</c>), over everything before it.</summary>
    private static void AdoptPageMetadata(MetadataCollection metadata, MetadataCollection? page)
    {
        if (page is null) return;
        if (!string.IsNullOrEmpty(page.Title)) metadata.Title = page.Title;
        foreach (var tag in page.Tags) metadata.AddOrUpdate(tag);
    }

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
    private static async Task ServePageState(HttpContext context, Type? page, DeclaredPage? declared)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        // Never cached: this is the page's data, and the next visitor's is not this one's.
        context.Response.Headers["Cache-Control"] = "no-store";
        // The answer to a navigation says so, with the header the request carried, whatever its
        // status: a page that answers 404 for content that does not exist still sends its title, its
        // head and its state, and the client applies them as a full load of it shows them. Read off
        // the status alone, the payload was dropped, and the previous page's head stayed.
        context.Response.Headers[NavigationHeader] = "1";

        // The document's metadata FIRST: the app's and the route's need nothing prepared, so a page
        // the server does not render (SSR off, a page that turns it off, a prefetch that failed)
        // still answers the title and the head a full load of it writes. It answered `{}`, and the
        // previous page's description and canonical stayed.
        var options = context.RequestServices.GetRequiredService<UIOptions>();
        var metadata = PageMetadata(context, options, declared);
        var prepared = await PreparedOrNull(context, options, page);
        if (prepared is not null)
        {
            // The page's own answer travels too — a route that matched while its content did not
            // exist says so to a client navigation the same way it says it to a full load.
            if (prepared.StatusCode != StatusCodes.Status200OK)
                context.Response.StatusCode = prepared.StatusCode;
            AdoptPageMetadata(metadata, prepared.Metadata);
        }
        LocalizeCanonical(context, options, metadata);

        var payload = new StringBuilder("{");
        payload.Append("\"title\":").Append(JsonSerializer.Serialize(metadata.Title));
        payload.Append(",\"head\":").Append(JsonSerializer.Serialize(metadata.RenderTags()));
        if (prepared?.SerializedState is { Length: > 0 } state)
            payload.Append(",\"state\":").Append(state);
        payload.Append('}');
        await context.Response.WriteAsync(payload.ToString());
    }

    /// <summary>
    /// The page's server-side moment without its drawing (prefetch, then its own metadata), or null
    /// where there is none: SSR off, no page, or a preparation that failed. A navigation must not be
    /// able to 500 the app: without the payload's state the page renders its empty state, which is
    /// exactly where it was before this endpoint existed.
    /// </summary>
    private static async Task<ServerRenderResult?> PreparedOrNull(HttpContext context, UIOptions options, Type? page)
    {
        var rendering = page is null || !options.EnableSsr
            ? null
            : context.RequestServices.GetService<IServerRenderingService>();
        if (rendering is null) return null;
        try
        {
            var result = await rendering.PreparePageAsync(page!, context);
            return result.Success ? result : null;
        }
        catch (Exception)
        {
            return null;
        }
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
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (map.IsDefault(culture) || map.SegmentFor(culture).Length == 0) return false;

        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";

        // The PATH decides whether the URL already names its language, and the route values do not.
        // This asked `RouteValues.ContainsKey("culture")`, which is only there when a culture-prefixed
        // route MATCHED — and a request that matched nothing at all, which is every 404, carries no
        // route values however plainly its path starts with /pt-BR. So an unknown path in a prefixed
        // language was redirected to PathFor(culture, path), which for an already-prefixed path is
        // that same path: six of seven languages had no 404 page at all, only a redirect to itself
        // until the browser gave up with ERR_TOO_MANY_REDIRECTS.
        if (!map.IsDefault(map.Split(path).Culture)) return false;

        context.Response.Redirect(map.PathFor(culture, path) + context.Request.QueryString, permanent: false);
        return true;
    }

    /// <summary>The theme's stylesheet for each mode a request can paint in, written once per theme:
    /// it depends on nothing else, and every page carries it.</summary>
    private static readonly ConditionalWeakTable<IAppTheme, string?[]> ThemeSheets = new();

    private static string ThemeStyles(IAppTheme theme, ThemeMode? mode)
    {
        var sheets = ThemeSheets.GetValue(theme, _ => new string?[3]);
        var slot = mode switch { ThemeMode.Light => 1, ThemeMode.Dark => 2, _ => 0 };
        return sheets[slot] ??= ThemeStylesheet.Generate(theme, mode);
    }

    private static async Task ServeAppShell(HttpContext context, Type? page, DeclaredPage? declared)
    {
        // A client navigation asks the same route for the page's data instead of a document.
        if (context.Request.Headers.ContainsKey(NavigationHeader))
        {
            await ServePageState(context, page, declared);
            return;
        }

        var options = context.RequestServices.GetRequiredService<UIOptions>();
        if (RedirectToLanguageUrl(context, options)) return;

        var shell = options.HtmlShell;
        // Per-request copy of the head tags. The HtmlShell is a singleton, so mutating
        // shell.HeadTags directly is not thread-safe and leaks tags across requests.
        var headTags = new List<string>(shell.HeadTags);

        // The theme's stylesheet, on EVERY page: the theme the app chose (options.UseTheme), or
        // Photon's, the one the browser runtime falls back to. It carries the document's rules and
        // every variable the lowered markup names, so a page whose app chose no theme is drawn from
        // one rather than left to the browser's defaults with variables nothing defines. The
        // template writes it before the app's own CSS, so an app's rule wins every tie with it.
        //
        // The mode THIS request paints in: what the visitor last chose, or the app's declared
        // default when they have not chosen. The cookie is written by the browser's own controller
        // on a toggle (see WebThemeController) — no round trip, and the server can read it, which
        // localStorage would never let it do. This is what removes the flash: the very first byte
        // already carries the right mode, instead of the default arriving and hydration correcting
        // it in front of the reader.
        var requestedMode = ThemeCookie.Resolve(context, options.InitialThemeMode, options.ThemeCookie?.Name);
        // The faces the theme and the app's catalogs ship, declared ahead of the sheet that names them.
        var fonts = context.RequestServices.GetRequiredService<ServedFonts>();
        var themeStyles = fonts.Rules + ThemeStyles(options.Theme ?? PhotonTheme.Instance, requestedMode);

        // A CHOSEN theme also crosses as data, into window.__EQ_THEME__, so boot can setPhotonTheme
        // before hydration and the client's re-renders resolve the colours and shapes the server
        // rendered. With none chosen the runtime already carries Photon's.
        var themeDataJson = options.Theme is { } appTheme ? ThemeBridge.SerializeJson(appTheme) : null;

        // Track L D4/D5: the request's culture rides the shell — <html lang> for assistive tech
        // (pronunciation follows it), and window.__EQ_CULTURE__ with the format culture's data and
        // the ACTIVE catalog inlined, so the client writes every number and date as the server wrote
        // them and resolves exactly the strings it rendered, before hydration. The format culture
        // travels on every page, catalog or not (#471). The culture itself is ASP.NET's answer
        // (RequestLocalization when the app wired it, the process default otherwise) — the SDK
        // only reads the statics, per the plan.
        var uiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        var formatCulture = System.Globalization.CultureInfo.CurrentCulture;
        var htmlLang = uiCulture.Name.Length > 0 ? uiCulture.Name : "en";
        var cultureDataJson = CultureBridge.BuildCultureData(context, uiCulture, formatCulture);

        // The app icon the SDK generated, linked without the app saying so. Stating it once — in
        // Assets/ — and having it appear in the tab, on a pinned home screen and in the install
        // manifest is the whole point; asking an author to also write four <link> tags would be
        // handing back the work we just took.
        headTags.AddRange(GeneratedIconTags(context));

        // What the document says before the page speaks (PageMetadata), restarted for an error page.
        var metadata = PageMetadata(context, options, declared);

        // Attempt SSR if page name is provided and SSR is enabled
        // The placeholder a page shows until its module mounts, when the server does not render it:
        // the class boot.ts writes too, styled by the theme's stylesheet.
        var ssrContent = "<div class=\"eq-loading\">Loading...</div>";
        // The page's paint servers, if it drew any — see GradientSink for why they are not inside
        // each drawing any more.
        string? vectorDefs = null;
        var ssrEnabled = false;
        // Whether rendering was attempted and BROKE, which is the only case the app's error page
        // answers. A page that opted out of server rendering shows its placeholder instead.
        var ssrFailed = false;
        string? serializedState = null;

        // ONE adoption for BOTH doors to the same page: the mapped route, and the fallback every
        // URL that matched nothing comes through. They were two copies of this and drifted — the
        // fallback quietly kept neither the payload nor the assets, so a not-found page that loads
        // its branding served it and then hydrated blank, unstyled. A local function instead of a
        // second copy, because the next thing a render result carries has one place to be adopted.
        void AdoptSsr(Rendering.ServerRenderResult adopted)
        {
            ssrContent = adopted.Html!;
            vectorDefs = adopted.VectorDefs;
            ssrEnabled = true;
            serializedState = adopted.SerializedState;

            AdoptPageMetadata(metadata, adopted.Metadata);

            // The page's atomic CSS rides here. Without it the fallback served the right markup
            // with none of its classes defined.
            if (adopted.Assets != null)
            {
                foreach (var tag in adopted.Assets.RenderTags())
                {
                    if (!headTags.Contains(tag))
                        headTags.Add(tag);
                }
            }
        }

        if (page != null && options.EnableSsr)
        {
            var renderingService = context.RequestServices.GetService<IServerRenderingService>();
            if (renderingService != null)
            {
                try
                {
                    var result = await renderingService.RenderPageAsync(page, context);
                    ssrFailed = result.Failed;
                    if (result.Success && result.Html != null)
                    {
                        AdoptSsr(result);

                        // What the PAGE asked to answer with (IHandleStatus). A route that matched
                        // while its content did not exist renders the right thing for a reader and
                        // must not tell every machine the request succeeded — a crawler would index
                        // the empty page and a link checker would call the site healthy.
                        //
                        // This one stays HERE and not in AdoptSsr: the fallback's status is already
                        // 404 from the endpoint, and a page answering 200 must not raise it back.
                        if (result.StatusCode != StatusCodes.Status200OK)
                            context.Response.StatusCode = result.StatusCode;
                    }
                }
                catch (Exception ex)
                {
                    ssrFailed = true;
                    var logger = context.RequestServices.GetService<ILogger<UIOptions>>();
                    logger?.LogWarning(ex, "SSR failed for page {PageName}, falling back to client-side rendering", page.FullName);
                }
            }
        }

        if (page == null)
        {
            // 404 Not Found Handling — the fallback endpoint already set the status; here the
            // app's registered /404 page (if any) takes over the CONTENT.
            // Only one the visitor may see: no endpoint carries its requirement on this path (#673).
            if (options.NotFoundPageType != null
                && await PageAuthorization.AllowsAsync(context, options.NotFoundPageType))
            {
                page = options.NotFoundPageType;
                
                // Try to render the 404 page via SSR
                if (options.EnableSsr)
                {
                    try 
                    {
                        var renderingService = context.RequestServices.GetService<IServerRenderingService>();
                        if (renderingService != null)
                        {
                            var result = await renderingService.RenderPageAsync(page, context);
                            if (result.Success && result.Html != null)
                            {
                                AdoptSsr(result);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // The 404 page failing must not fail the 404, so this still swallows — but
                        // SILENTLY is how the missing line above survived: a page that threw here
                        // looked exactly like a page that rendered.
                        context.RequestServices.GetService<ILoggerFactory>()?
                            .CreateLogger("eQuantic.UI.NotFound")
                            .LogWarning(ex, "The registered /404 page failed to render; serving the shell without it");
                    }
                }
            }
        }

        // 500 Error Handling during SSR.
        //
        // NOT when the answer is already 404. Reaching here with a 404 means the app's own
        // not-found page failed to render, and the request is still what it was: a URL that
        // matches nothing. Upgrading it to 500 tells every crawler and link checker the server
        // broke, when what happened is the resource does not exist — and it undoes the contract
        // the branch above exists to hold, that a 404 page failing must not fail the 404.
        var alreadyNotFound = context.Response.StatusCode == StatusCodes.Status404NotFound;

        if (!ssrEnabled && page != null && options.EnableSsr && !alreadyNotFound)
        {
            // The page was not rendered here: either it opted out of server rendering, and shows its
            // placeholder until its module mounts, or its render BROKE, and in production the app's
            // error page answers for it. Only the second is a 500. This used to read the placeholder's
            // text to decide, which both cases carry, so a client-only page in an app with a /500 page
            // was answered with the error page.
            var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            if (!isDev && options.ErrorPageType != null && ssrFailed
                && await PageAuthorization.AllowsAsync(context, options.ErrorPageType))
            {
                // Re-attempt SSR with the 500 page
                try
                {
                    var errorPage = options.ErrorPageType;
                    var renderingService = context.RequestServices.GetService<IServerRenderingService>();
                    if (renderingService != null)
                    {
                         var result = await renderingService.RenderPageAsync(errorPage, context);
                         if (result.Success && result.Html != null)
                         {
                             context.Response.StatusCode = 500;
                             page = errorPage;
                             // The error page is not the route that failed: it speaks over the
                             // app's defaults, never under the failed route's title.
                             metadata = PageMetadata(context, options, null);
                             // The THIRD door to a rendered page, and it was drifting like the other
                             // two: an error page that loads its own branding kept none of it.
                             AdoptSsr(result);
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

        // The app's route table and whether it has server actions, worked out once (AppSurface).
        var surface = AppSurface.Of(options, route => CultureClientPatterns(options, route));

        // The client's configuration, written by System.Text.Json and nothing else. Its strings were
        // quoted by hand, escaping the backslash and the quote, so a route title holding a line break
        // was a syntax error in a script every page carries, and stopped the client of the whole app
        // (#526); a `</script>` closed the element, and the theme cookie's name went in raw. The
        // serializer's default encoder escapes every code unit a script cannot carry, `<` among them,
        // and JSON is a JavaScript expression.
        var configJson = JsonSerializer.Serialize(new ClientConfig(
            // The page's MODULE, which the build names after the type and the types it is nested in
            // (TwinName.OfType, #584), so a page declared inside a class loads the module eqc wrote.
            Page: page is null ? null : TwinName.OfType(page),
            Version: BuildId,
            Ssr: ssrEnabled,
            // Whether this server streams rebuilds, so the page listens exactly when there is a stream.
            HotReload: options.HotReloads(context.RequestServices.GetRequiredService<IWebHostEnvironment>()),
            // What the page was rendered at: hydration adopts this markup, so it lowers at the same
            // density and then switches the whole page to the browser's own at once (#623).
            Density: DensityCookie.NameOf(DensityCookie.Resolve(context)),
            // The cookie config crosses to the browser because the browser is what WRITES it while the
            // server READS it. Two places to configure would drift, and a drifted name fails silently:
            // the server reads a cookie nobody writes, so persistence stops while everything still
            // looks right. `false` is the app having turned it off.
            ThemeCookie: options.ThemeCookie is { } cookie ? new ClientThemeCookie(cookie.Name, cookie.Days) : false,
            // The language-prefix policy crosses because the browser has to APPLY it: an href lowered
            // after hydration, and the switcher's own navigation, both need to know which segments are
            // languages. Guessing by shape would call a page named `pt` a language.
            CultureRoutes: options.CultureRoutes is { } cultureMap
                ? new ClientCultureRoutes(cultureMap.Default, cultureMap.Prefixed.ToList())
                : null,
            Routes: surface.Routes,
            // Whether the server serves events crosses because only the server knows: without it, a
            // page whose app never called UseServerEvents opened a stream the app's fallback
            // answered, and retried it forever while no subscription heard why.
            ServerEvents: options.ServerEvents is not null), ClientConfig.Json);

        // Render HTML using template engine with conditionals
        var isDevelopment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var hasServerActions = surface.HasServerActions;

        var template = HtmlTemplateEngine.FromResource("eQuantic.UI.Server.Templates.app-shell.html");
        var html = template.Render(ctx =>
        {
            // Variables
            ctx.Set("HtmlClass", shell.HtmlClass)
               .Set("HtmlLang", htmlLang)
               .Set("Title", System.Web.HttpUtility.HtmlEncode(metadata.Title ?? string.Empty))
               .Set("MetadataTags", metadata.RenderTags())
               .SetOrEmpty("FontPreloads", fonts.Preloads)
               .Set("BuildId", BuildId)
               .Set("ThemeStyles", themeStyles)
               // One element per AddStyles call, so a malformed chunk cannot swallow the next one.
               .SetOrEmpty("AppStyles", string.Join("\n        ", shell.Styles.Select(css => $"<style>{css}</style>")))
               .Set("HeadTags", string.Join("\n    ", headTags))
               .Set("SsrEnabled", ssrEnabled.ToString().ToLowerInvariant())
               .Set("SsrContent", ssrContent)
               .SetOrEmpty("VectorDefs", vectorDefs)
               .Set("ConfigJson", configJson)
               .Set("IsDevelopmentBool", isDevelopment ? "true" : "false")
               .SetOrEmpty("InitialState", serializedState != null ? $"window.__INITIAL_STATE__ = {serializedState};" : null)
               .SetOrEmpty("ThemeData", themeDataJson != null ? $"window.__EQ_THEME__ = {themeDataJson};" : null)
               .SetOrEmpty("CultureData", $"window.__EQ_CULTURE__ = {cultureDataJson};");

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
