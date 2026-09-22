using eQuantic.UI.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Web;
using eQuantic.UI.Server.Assets;
using eQuantic.UI.Server.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// Default implementation of <see cref="IServerRenderingService"/>.
/// Provides server-side rendering for eQuantic.UI components.
/// </summary>
public class ServerRenderingService : IServerRenderingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UIOptions _options;
    private readonly ILogger<ServerRenderingService> _logger;
    private readonly Dictionary<string, Type> _pageTypes = new();

    public ServerRenderingService(
        IServiceProvider serviceProvider,
        UIOptions options,
        ILogger<ServerRenderingService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;

        // Scan assemblies for page types
        ScanPageTypes();
    }

    /// <summary>Whether a DEVELOPER is on the other end of this request — what decides if a
    /// contained failure quotes its exception or stays generic.</summary>
    private static bool IsDevelopment(HttpContext context) =>
        context.RequestServices.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()
            ?.EnvironmentName == "Development";

    private void ScanPageTypes()
    {
        foreach (var assembly in _options.AssembliesToScan)
        {
            try
            {
                var pageTypes = assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes<PageAttribute>().Any() &&
                               !t.IsAbstract &&
                               // WRITE-ONCE pages (Photon vocabulary) — SSR bridges them through
                               // the web realizer (see CreateComponentInstance).
                               typeof(Primitives.UiComponent).IsAssignableFrom(t));

                foreach (var type in pageTypes)
                {
                    _pageTypes[type.Name] = type;
                    _logger.LogDebug("Registered page type for SSR: {PageType}", type.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan assembly {Assembly} for page types", assembly.FullName);
            }
        }

        // Pages routed from Program.cs with MapPage<T> rather than a [Page] attribute. They are
        // indexed by the same name the endpoint serves, so everything downstream cannot tell the
        // two ways of declaring a route apart — which is the point.
        foreach (var (_, page, _) in _options.DeclaredRoutes)
        {
            _pageTypes[page.Name] = page;
        }

        _logger.LogInformation("SSR initialized with {Count} page types", _pageTypes.Count);
    }

    /// <inheritdoc />
    public Task<ServerRenderResult> RenderPageAsync(string pageTypeName, HttpContext context) =>
        RunAsync(pageTypeName, context, draw: true);

    /// <inheritdoc />
    public Task<ServerRenderResult> PreparePageAsync(string pageTypeName, HttpContext context) =>
        RunAsync(pageTypeName, context, draw: false);

    /// <summary>
    /// The page's whole server-side moment, with the DRAWING optional.
    /// <para>
    /// Both callers need the same three things in the same order — prefetch, then metadata, then
    /// the status the page answers — from the request's own services. Only one of them needs the
    /// markup, and building a tree to throw it away is what made a client navigation cost as much
    /// as a full page.
    /// </para>
    /// </summary>
    private async Task<ServerRenderResult> RunAsync(string pageTypeName, HttpContext context, bool draw)
    {
        if (string.IsNullOrEmpty(pageTypeName))
        {
            return ServerRenderResult.NotAvailable();
        }

        if (!_pageTypes.TryGetValue(pageTypeName, out var pageType))
        {
            _logger.LogWarning("Page type not found for SSR: {PageType}", pageTypeName);
            return ServerRenderResult.NotAvailable();
        }

        try
        {
            // Check if SSR is disabled for this page
            var pageAttr = pageType.GetCustomAttributes<PageAttribute>().FirstOrDefault();
            if (pageAttr?.DisableSsr == true)
            {
                _logger.LogDebug("SSR disabled for page: {PageType}", pageTypeName);
                return ServerRenderResult.NotAvailable();
            }

            // Create the component instance with DI
            var component = CreateComponentInstance(pageType, context.RequestServices);

            object metadataSource = component is Web.VisualNodeComponent bridge ? bridge.Node : component;

            // Scoped provider + route for this async context (thread-safe via AsyncLocal), armed
            // BEFORE anything the page runs. The prefetch is the first thing that needs the route:
            // a page on /docs/{slug} loads BY the slug, so a route arriving after it would be a
            // route arriving after the only question it was there to answer.
            // ONE route, in the shape with no target in it — a write-once page reads
            // context.Route.Param("slug") instead of reaching for ASP.NET (and losing Photon), and
            // the web's `context.Route` is this same value rather than a copy of it.
            Primitives.RouteValues.Current = BuildRouteValues(context);
            // Where a component in the MIDDLE of the tree finds a capability — the REQUEST's
            // container, so a scoped one resolves and a page's own registrations win.
            Primitives.CapabilityScope.Current = context.RequestServices.GetService;
            // Every in-app href picks up THIS request's language prefix. The culture is the one
            // UseRequestLocalization negotiated — from the path segment first — so a page served
            // at /pt-BR/pricing links to /pt-BR/about without any page saying so.
            if (_options.CultureRoutes is { } cultureRoutes)
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                RenderContext.SetLinkPolicy(path => cultureRoutes.PathFor(culture, path));
            }

            try
            {
                // SERVER DATA (IServerPrefetch) for EVERY component in the tree, not just the root.
                // The root used to be the only one asked, while the contract said any component the
                // page composes may declare server data — so a header composed into every route drew
                // the right value during SSR and blanked the moment hydration rebuilt it from a
                // payload that never carried the field.
                //
                // THE DRAWING IS ROUND ZERO, and that is a measurement rather than a preference. For
                // a write-once page the components do not exist until the realizer builds them, so
                // discovery needs an expansion — and doing it BEFORE the drawing cost every page 20%
                // of its SSR time, prefetch or not: a page declaring no server data at all paid for a
                // walk that could only ever find nothing. Drawing first means such a page renders
                // exactly once, as it always did, and only a page that actually loads something draws
                // again.
                var prefetched = new Dictionary<string, Web.LoadedComponentState>(StringComparer.Ordinal);
                var asked = new HashSet<string>(StringComparer.Ordinal);

                // A CORE page is an IComponent tree, not a write-once one, so it never passes through
                // the realizer's component visit and the walk below would find nothing to ask. Its
                // root is asked directly, which is what the pipeline did for every page before this.
                // It reaches the index only through MapPage<T>: the attribute scan admits write-once
                // pages alone, while MapPage accepts either.
                Primitives.IServerPrefetch? coreRoot = null;
                string? coreRootKey = null;
                if (metadataSource is not Primitives.UiComponent && metadataSource is Primitives.IServerPrefetch root)
                {
                    coreRoot = root;
                    // ITS KEY JOINS THE ASKED SET, or the page ships no payload at all: `asked` is
                    // what decides whether a response carries state, so a Core root loading on the
                    // server and then hydrating from its defaults was the one page shape that never
                    // needed this walk and was the only one left reverting. Its ordinal is 0 by
                    // construction — the root is the first component either side names.
                    coreRootKey = $"{metadataSource.GetType().Name}#0";
                    asked.Add(coreRootKey);
                    await coreRoot.PrefetchAsync(context.RequestServices, context.RequestAborted);
                }

                // THE DRAWING, and everything that only the drawing needs. A client navigation asks
                // for none of it: the browser already has the component and builds the tree itself,
                // so markup rendered here would be markup thrown away — a whole tree build, plus its
                // assets and its atomic rules, per navigation.
                // The wire form of what a NAVIGATION loaded. A drawing reads its payload off the
                // instances that drew; a navigation has none, so the walk keeps it as it goes.
                Dictionary<string, IReadOnlyDictionary<string, object?>>? navigationPayload = null;

                var assets = new AssetCollection();
                var html = string.Empty;
                // Out here with the html and the paint servers, and for the same reason: the
                // payload is built past the end of the drawing block, from the components that drew.
                Web.ComponentExpansionScope? renderScope = null;
                // Declared out here for the same reason the html is: a page that did not draw has
                // no paint servers to declare, and the result is built past the end of this block.
                string? vectorDefs = null;
                if (draw)
                {
                    // The loop repeats because the SET of components can depend on the data: what a
                    // page composes after loading a list is not knowable before loading it. It ends
                    // the moment a round finds no component it has not already asked, which for a
                    // page with no prefetch at all is the first one.
                    for (var round = 0; round < MaxPrefetchRounds; round++)
                    {
                        assets = new AssetCollection();
                        vectorDefs = null;
                        CollectAssets(component, assets, context.RequestServices, new HashSet<Type>());

                        // Render with an AMBIENT style sink armed (STYLE-SEMANTICS-PLAN §2): every
                        // write-once bridge in the tree — top-level page or composed inside a Core
                        // component — collects its atomic rules into this one per-page set.
                        var styles = new Web.StyleSink();
                        Web.StyleSink.Ambient = styles;
                        // The same shape, for the same reason, one layer over: every gradient the page
                        // paints with, declared ONCE for the document instead of once per drawing.
                        var gradients = new Web.GradientSink();
                        Web.GradientSink.Ambient = gradients;
                        // Armed the same way and for the same reason: a component that throws is
                        // contained to its own subtree instead of turning this request into a 500.
                        // Development quotes the exception in the panel; production says only that a
                        // section is missing, and either way the failure reaches the log.
                        Primitives.ComponentBoundary.Diagnostics = IsDevelopment(context);
                        Primitives.ComponentBoundary.Report = (failed, error) => _logger.LogError(error,
                            "Component {Component} failed to render and was contained", failed.GetType().Name);
                        // NAMES every component as the realizer expands it, and hands back what an
                        // earlier round loaded for it — before its Build runs, or the build reads the
                        // defaults again and the round was wasted.
                        renderScope = new Web.ComponentExpansionScope { Restore = prefetched };
                        Web.ComponentExpansionScope.Ambient = renderScope;
                        try
                        {
                            html = RenderComponent(component);
                        }
                        finally
                        {
                            Web.ComponentExpansionScope.Ambient = null;
                            Web.StyleSink.Ambient = null;
                            Web.GradientSink.Ambient = null;
                            Primitives.ComponentBoundary.Report = null;
                        }

                        // Inject exactly the rules this markup references, so hydration matches by class
                        // identity. The id lets the client registry adopt them instead of re-inserting.
                        if (!styles.IsEmpty)
                        {
                            assets.Add(new InlineStyleAsset(styles.Css, "eq-atomic"));
                        }

                        // The page's own paint servers, rendered the same way the page was.
                        if (!gradients.IsEmpty) vectorDefs = RenderComponent(gradients.Container());

                        // ASKED ONCE, except where the key now names a DIFFERENT component: a page
                        // whose own data chooses what it composes can put another row at the same
                        // key, and what loaded there belongs to the row that is gone. The scope
                        // refuses that restore, so the new one is asked for its own data rather
                        // than drawing someone else's.
                        var pending = renderScope.Expanded
                            .Where(pair => pair.Value is Primitives.IServerPrefetch
                                && (asked.Add(pair.Key) || renderScope.Replaced.Contains(pair.Key)))
                            .ToList();

                        // THE MARKUP THIS ROUND PRODUCED IS THE ANSWER when nothing new asked for
                        // data — which is every page that declares none, on its first pass.
                        if (pending.Count == 0) break;

                        // NOT THIS ROUND, because nothing would draw what it loads. The loop renders
                        // at the TOP, so awaiting on the last allowed pass would store values the
                        // markup never shows and the payload would disagree with the page beside it.
                        // Stopping here serves the defaults in BOTH, which is a visibly incomplete
                        // page rather than a page whose words and state contradict each other.
                        if (round == MaxPrefetchRounds - 1)
                        {
                            _logger.LogWarning(
                                "[SSR Prefetch] Stopped at {Rounds} rounds with {Count} component(s) still "
                                + "asking for data ({Replaced} of them replaced rather than new); they "
                                + "render their defaults. A chain this deep usually means a component "
                                + "loads what another one had to load first — or, when components keep "
                                + "being replaced, that one of them differs every time it is built.",
                                MaxPrefetchRounds, pending.Count,
                                pending.Count(p => renderScope.Replaced.Contains(p.Key)));
                            break;
                        }

                        // ONE AT A TIME. They were started together at first, which is faster and
                        // wrong: every prefetch is handed the REQUEST's service provider, so two
                        // components resolving the same scoped dependency — an EF DbContext being
                        // the ordinary case — would use one instance concurrently, which EF refuses
                        // by design. A page that worked would fail for a reason its author could not
                        // see. Parallelism here is worth having only behind a contract that says
                        // prefetch dependencies are safe for concurrent use, and there is no such
                        // contract today.
                        foreach (var pair in pending)
                        {
                            // BEFORE and after, because what travels is the DELTA. Writing every
                            // field back onto the next round's instance overwrote what its own
                            // constructor had just been given.
                            var asBuilt = Web.ComponentExpansionScope.Capture(pair.Value);
                            await ((Primitives.IServerPrefetch)pair.Value).PrefetchAsync(
                                context.RequestServices, context.RequestAborted);
                            // THE CLR VALUES, not the wire ones: this is restored onto the fresh
                            // instances the next expansion creates, and the payload's normalization
                            // (enums to strings, backing fields to property names) would make every
                            // one of those unassignable.
                            prefetched[pair.Key] =
                                Web.ComponentExpansionScope.WhatLoaded(pair.Value, asBuilt);
                        }
                    }
                }
                else if (metadataSource is Primitives.UiComponent)
                {
                    // A navigation produces no markup, so there is no drawing to discover through —
                    // the walk has to expand on its own. It still costs only the pages that have
                    // something to find, because a navigation is answering with state in the first
                    // place.
                    navigationPayload = await PrefetchTreeAsync(component, prefetched, asked, context);
                }

                // THEN metadata, and the order is the whole point. ConfigureMetadata used to run first,
                // so a page could only state what it knew before seeing the request — which for one page
                // serving many documents means every one of them emitting the SAME canonical, title and
                // description. Duplicate canonicals across seventy URLs are worse for a crawler than no
                // canonical at all: it is the page asserting they are one document.
                //
                // Running after the prefetch lets metadata be built from what the prefetch loaded, which
                // is the only order in which a data-driven page can describe itself.
                MetadataCollection? metadata = null;
                if (metadataSource is IHandleMetadata metadataHandler)
                {
                    metadata = new MetadataCollection();
                    metadataHandler.ConfigureMetadata(new SeoBuilder(metadata));
                }

                // Serialize state for hydration. A write-once page carries its state in its OWN
                // fields (there is no separate state object), and the transpiled twin declares the
                // same field names — so the payload crosses by name.
                // EVERY component that prefetched, read off the instances that actually DREW —
                // renderScope holds those, so the payload cannot disagree with the markup beside it.
                string? serializedState = null;
                if (asked.Count > 0)
                {
                    var payload = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
                    foreach (var key in asked)
                    {
                        // OFF THE INSTANCE THAT DREW, or not at all. A component discovered in an
                        // earlier round but absent from the final tree has nothing on the page for
                        // its state to belong to, and shipping it would hand the client a key its
                        // own walk never reaches.
                        if (key == coreRootKey && coreRoot is not null)
                        {
                            // A Core root is in no scope's Expanded — it never passes through the
                            // realizer's component visit — but it IS the instance that drew: the
                            // pipeline renders that object and never rebuilds it. Read here rather
                            // than beside the load, so what ships is the state the markup left.
                            payload[key] = Snapshot(coreRoot);
                        }
                        else if (renderScope is not null && renderScope.Expanded.TryGetValue(key, out var drew))
                        {
                            payload[key] = Snapshot(drew);
                        }
                        else if (navigationPayload is not null
                            && navigationPayload.TryGetValue(key, out var loaded))
                        {
                            // A navigation draws nothing, so there is no rendered instance to read —
                            // the walk kept the wire form of each component as it loaded.
                            payload[key] = loaded;
                        }
                    }
                    serializedState = SerializeState(payload);
                }

                _logger.LogDebug("SSR completed for page: {PageType}, HTML length: {Length}",
                    pageTypeName, html.Length);

                // The page's own answer, asked AFTER the prefetch — "does this exist" is something
                // a page usually learns by loading it. A matched route with no content behind it
                // renders fine and must not answer 200.
                var status = metadataSource is Primitives.IHandleStatus statusHandler
                    ? statusHandler.StatusCode
                    : 200;

                return ServerRenderResult.Ok(html, metadata, serializedState,
                    assets.HasAssets ? assets : null, status, vectorDefs);
            }
            finally
            {
                RenderContext.SetLinkPolicy(null);
                Primitives.RouteValues.ClearCurrent();
                Primitives.CapabilityScope.Current = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSR failed for page: {PageType}", pageTypeName);
            // Reflection wraps whatever a constructor threw in "Exception has been thrown by the
            // target of an invocation", which names nothing. The developer needs the message the
            // page's own code wrote, so unwrap to it.
            var reported = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            return ServerRenderResult.Fail(reported.Message);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// How many times the tree may be expanded looking for new prefetchers. Two is the ordinary
    /// answer — one round finds them, the next confirms nothing new appeared — and more than two
    /// only happens when a component EXISTS because of what another one loaded, which is the case
    /// a single round cannot see: a page that loads a list and then composes one component per row,
    /// each declaring its own server data. A component the data REPLACED costs one more, since what
    /// loaded under its key belonged to the one it replaced and it has to be asked for its own. The
    /// cap is what stops a tree that keeps growing from holding a request open; past it the page
    /// draws with what it has rather than failing, because a missing value is recoverable and a
    /// hung request is not.
    /// </summary>
    private const int MaxPrefetchRounds = 5;

    /// <summary>
    /// Loads the server data for EVERY <see cref="Primitives.IServerPrefetch"/> the page expands,
    /// and returns it keyed by component so the render below — and the client after it — can hand
    /// each value back to the component it belongs to.
    ///
    /// <para>
    /// It costs an expansion, and the reason is structural rather than lazy: for a write-once page
    /// the component tree does not exist until the realizer builds it, and the realizer is
    /// synchronous while a prefetch is not. So the tree is expanded to find out WHO wants data, the
    /// loads are awaited ONE AT A TIME (they share the request's service provider — the loop below
    /// says why), and the next expansion runs with the values restored. The discovery expansion
    /// builds no HTML and no string — it stops at the realized element.
    /// </para>
    ///
    /// <para>
    /// The loop repeats because the SET of components can depend on the data: what a page composes
    /// after loading a list is not knowable before loading it. It ends the moment a round finds no
    /// component it has not already asked.
    /// </para>
    /// </summary>
    private async Task<Dictionary<string, IReadOnlyDictionary<string, object?>>> PrefetchTreeAsync(
        IComponent component,
        Dictionary<string, Web.LoadedComponentState> loaded,
        HashSet<string> asked,
        HttpContext context)
    {
        var wire = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

        for (var round = 0; round < MaxPrefetchRounds; round++)
        {
            var scope = new Web.ComponentExpansionScope { Restore = loaded };
            Expand(component, scope);

            var pending = scope.Expanded
                .Where(pair => pair.Value is Primitives.IServerPrefetch
                    && (asked.Add(pair.Key) || scope.Replaced.Contains(pair.Key)))
                .ToList();

            if (pending.Count == 0) break;

            if (round == MaxPrefetchRounds - 1)
            {
                _logger.LogWarning(
                    "[SSR Prefetch] Stopped at {Rounds} rounds with {Count} component(s) still asking "
                    + "for data; the navigation carries their defaults.", MaxPrefetchRounds, pending.Count);
                break;
            }

            // ONE AT A TIME, for the reason the drawing loop states: they share the REQUEST's
            // service provider, and two components resolving one scoped dependency would use it
            // concurrently.
            foreach (var pair in pending)
            {
                // BEFORE and after, because what travels is the DELTA — the drawing loop says why.
                var asBuilt = Web.ComponentExpansionScope.Capture(pair.Value);
                await ((Primitives.IServerPrefetch)pair.Value).PrefetchAsync(
                    context.RequestServices, context.RequestAborted);

                // TWO FORMS of the same instance, taken while it still holds what it just loaded:
                // the CLR one to restore onto the next round's fresh instance, and the wire one for
                // the response. A navigation draws nothing, so there is no rendered instance to read
                // the payload from later.
                loaded[pair.Key] = Web.ComponentExpansionScope.WhatLoaded(pair.Value, asBuilt);
                wire[pair.Key] = Snapshot(pair.Value);
            }
        }

        return wire;
    }

    /// <summary>
    /// One discovery expansion: builds the tree and throws the result away, with its own style and
    /// gradient sinks so nothing it collects reaches the page the request actually serves.
    /// </summary>
    private static void Expand(IComponent component, Web.ComponentExpansionScope scope)
    {
        var styles = Web.StyleSink.Ambient;
        var gradients = Web.GradientSink.Ambient;
        Web.StyleSink.Ambient = new Web.StyleSink();
        Web.GradientSink.Ambient = new Web.GradientSink();
        Web.ComponentExpansionScope.Ambient = scope;
        try
        {
            component.Render();
        }
        finally
        {
            Web.ComponentExpansionScope.Ambient = null;
            Web.StyleSink.Ambient = styles;
            Web.GradientSink.Ambient = gradients;
        }
    }

    public string RenderComponent(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        return HtmlRenderer.RenderToString(component);
    }

    /// <inheritdoc />
    public bool IsSsrEnabled(string pageTypeName)
    {
        if (string.IsNullOrEmpty(pageTypeName))
            return false;

        if (!_pageTypes.TryGetValue(pageTypeName, out var pageType))
            return false;

        var pageAttr = pageType.GetCustomAttributes<PageAttribute>().FirstOrDefault();
        return pageAttr?.DisableSsr != true;
    }

    /// <summary>
    /// Recursively walks the component tree to collect asset dependencies.
    /// </summary>
    private void CollectAssets(IComponent component, AssetCollection assets, IServiceProvider services, HashSet<Type> visited)
    {
        var type = component.GetType();

        // Check IRequireAssets and DI providers (only once per type for dedup)
        if (visited.Add(type))
        {
            if (component is IRequireAssets requireAssets)
            {
                requireAssets.ConfigureAssets(new AssetBuilder(assets));
            }

            // Check DI for IComponentAssetProvider<T>
            var providerType = typeof(IComponentAssetProvider<>).MakeGenericType(type);
            var provider = services.GetService(providerType);
            if (provider != null)
            {
                var method = providerType.GetMethod("ConfigureAssets");
                method?.Invoke(provider, new object[] { new AssetBuilder(assets) });
            }
        }

        // Recurse into children
        foreach (var child in component.Children)
        {
            CollectAssets(child, assets, services, visited);
        }
    }

    /// <summary>
    /// Builds the page, taking its constructor dependencies from THIS REQUEST's services.
    /// <para>
    /// The request's container, not the application's: a page whose constructor takes anything
    /// registered scoped — a DbContext, a unit of work, the current tenant, which is most of what a
    /// page actually depends on — cannot be built from the root provider at all. .NET refuses it by
    /// design, because a scoped service resolved from the root outlives the request and is then
    /// shared by every later one.
    /// </para>
    /// </summary>
    private IComponent CreateComponentInstance(Type componentType, IServiceProvider services)
    {
        object? instance;
        try
        {
            instance = ActivatorUtilities.CreateInstance(services, componentType);
        }
        catch (InvalidOperationException) when (componentType.GetConstructor(Type.EmptyTypes) is not null)
        {
            // No constructor could be satisfied AND the page declares it needs nothing: build it.
            // Narrow on purpose — the catch here used to be bare, so a page whose own constructor
            // threw was quietly rebuilt with nothing injected and rendered as if it had asked for
            // nothing. The dependency was null, the page drew its empty state, and the exception
            // that explained it was gone.
            instance = Activator.CreateInstance(componentType);
        }

        if (instance is IComponent component)
        {
            return component;
        }

        // A WRITE-ONCE page (eQuantic.UI.Primitives.UiComponent) is not an IComponent — bridge it
        // through the web-realizer adapter so the SSR pipeline stays IComponent-only. The client
        // needs no counterpart: the transpiled page extends StatefulComponent, which mounts/
        // hydrates directly (v1 fence: no server-driven initial state — field defaults render).
        if (instance is Primitives.UiComponent visual)
        {
            return new Web.VisualNodeComponent(visual, _options.Theme);
        }

        throw new InvalidOperationException($"Cannot create instance of component type: {componentType.Name}");
    }

    /// <summary>
    /// Builds the per-request <see cref="Primitives.RouteValues"/> from the HTTP route values and
    /// query string, so SSR sees the same parameters the client router will (e.g. <c>id</c> in
    /// <c>/users/{id}</c>).
    /// </summary>
    private static Primitives.RouteValues BuildRouteValues(HttpContext context)
    {
        var routeParams = new Dictionary<string, string>();
        foreach (var rv in context.Request.RouteValues)
        {
            if (rv.Value is not null)
                routeParams[rv.Key] = rv.Value.ToString() ?? string.Empty;
        }

        // A REPEATED key means its FIRST value — `?tag=a&tag=b` is `a`. `StringValues.ToString()`
        // comma-JOINS ("a,b"), which is ASP.NET's own spelling and not a thing a page asking for
        // `Query("tag")` can use; the client answered the first value, from `URLSearchParams.get`.
        // Two sides, two answers, neither written down. This is the one both keep now, and
        // `RouteValuesQueryPolicyTests` holds them to it. Found in review.
        var query = new Dictionary<string, string>();
        foreach (var q in context.Request.Query)
        {
            query[q.Key] = q.Value.Count > 0 ? q.Value[0] ?? string.Empty : string.Empty;
        }

        return new Primitives.RouteValues(routeParams, query);
    }

    /// <summary>
    /// An interface from anywhere but System — the SAME rule the compiler applies when it decides
    /// a constructor parameter is a capability to resolve rather than a value to pass. The
    /// normative statement is <c>CapabilityRule.IsDependency</c> in <c>src/Shared</c>; it reads a
    /// Roslyn <c>ITypeSymbol</c> and cannot be called here, so the two must be kept saying the
    /// same thing.
    /// <para>
    /// The System exclusion is the whole point and the comment on the shared rule says why: the
    /// first version of it dropped <c>IReadOnlyList&lt;AccordionItem&gt;</c>, which is how a
    /// component RECEIVES its items. Treating that as a dependency here would silently delete
    /// state from the payload — the exact failure this method exists to stop, arriving from the
    /// other side.
    /// </para>
    /// </summary>
    private static bool IsDependency(Type type) =>
        type.IsInterface
        && type.Namespace is { } space
        && !space.StartsWith("System", StringComparison.Ordinal);

    /// <summary>
    /// Serializes component state to JSON for client-side hydration.
    /// </summary>
    /// <summary>
    /// ONE component's fields as the payload carries them — and as the discovery loop restores them
    /// onto the next round's fresh instance, which is the same question asked twice.
    /// </summary>
    private IReadOnlyDictionary<string, object?> Snapshot(object state)
    {
        try
        {
            // Use reflection to extract all fields (including private) into a dictionary
            var stateDict = new Dictionary<string, object?>();

            // ITS BASES INCLUDED. GetFields alone does not return a base type's PRIVATE fields, so
            // a page keeping its loaded value in a private field on a base class had it dropped
            // from the payload in silence — the value drew and then vanished on hydration, the same
            // symptom the dependency guard below describes. Unreachable until a component over an
            // app-owned base became usable at all; reachable now, so it is fixed here.
            var fields = Web.ComponentExpansionScope.FieldsOf(state.GetType());
            
            foreach (var field in fields)
            {
                // A DEPENDENCY, not data. A constructor parameter whose type is a dependency is
                // captured as a field, and the client resolves it for itself — the emitter writes
                // `this.clock = $eq.services.resolve('IClock')` for exactly these — so it must
                // never ride the payload. It also cannot: serializing an IMediator threw inside
                // the single Serialize below, and the catch dropped the WHOLE page's state.
                //
                // The symptom was the worst kind. The server rendered correctly, hydration rebuilt
                // from an empty payload, and what the prefetch had loaded vanished in front of the
                // reader a moment after the page appeared. `curl` sees perfect HTML; only a
                // browser shows it.
                if (IsDependency(field.FieldType))
                {
                    _logger.LogDebug(
                        "[SSR Hydration] Skipping {FieldName}: a {TypeName} is a dependency the "
                        + "client resolves, not state to carry", field.Name, field.FieldType.Name);
                    continue;
                }

                var fieldName = field.Name;
                
                // For auto-properties, the backing field name is typically <PropertyName>k__BackingField
                // We want to use the PropertyName as the key in the JSON
                if (fieldName.StartsWith("<") && fieldName.Contains(">k__BackingField"))
                {
                    fieldName = fieldName.Substring(1, fieldName.IndexOf(">") - 1);
                }
                else if (fieldName.StartsWith("_"))
                {
                    // but we can strip the underscore to be more consistent with JS if needed.
                    // Actually, let's keep them so the JS side can match the field name if it's there.
                }

                var value = field.GetValue(state);

                // A HANDLER NEVER TRAVELS — the client builds its own. Read off the FIELD's type as
                // well as the value's, because a value says nothing about its type when it is null,
                // and a null ships now.
                if (typeof(Delegate).IsAssignableFrom(field.FieldType) || value is Delegate)
                {
                    continue;
                }

                // A NULL SHIPS, and this is the line that used to drop it. Omitting the field made
                // CLEARING a non-null default indistinguishable from loading nothing at all: the
                // server drew the cleared value, the payload said nothing, and the client kept the
                // default it was constructed with — the page reverting in front of the reader a
                // moment after it appeared, which is the failure this walk exists to remove wearing
                // the one shape that looks like absence.
                if (value is null)
                {
                    stateDict[fieldName] = null;
                    continue;
                }

                // Convert enums to lowercase string (matching JS compilation)
                if (value.GetType().IsEnum)
                {
                    var enumName = value.ToString() ?? "";
                    _logger.LogDebug("[SSR Enum] Converting enum {FieldName}: {Value} -> '{EnumName}'", fieldName, value, enumName);

                    if (!string.IsNullOrEmpty(enumName))
                    {
                        var jsEnumValue = char.ToLowerInvariant(enumName[0]) + enumName.Substring(1);
                        stateDict[fieldName] = jsEnumValue;
                    }
                    else
                    {
                        stateDict[fieldName] = "0";
                    }
                }
                else
                {
                    stateDict[fieldName] = value;
                }
            }

            // EqJson serializes Int64/UInt64 as strings so values beyond 2^53 survive into the
            // client BigInt-backed `long` runtime (matches the Server Action wire protocol).
            var options = eQuantic.UI.Server.Json.EqJson.Options;

            // FIELD BY FIELD, so one value that cannot be written does not take the rest with it.
            // The interface guard above catches the common case by design; this catches the rest
            // by construction — a concrete type nobody thought of stays a missing field instead of
            // an empty page, and says so in the log rather than in silence.
            foreach (var (key, value) in stateDict.ToList())
            {
                try
                {
                    // To NOWHERE. The question is only "does this throw?", and answering it with
                    // Serialize(value) builds a string per field per render that is read once and
                    // dropped — on a page whose payload is then written a second time anyway.
                    using var writer = new System.Text.Json.Utf8JsonWriter(Stream.Null);
                    System.Text.Json.JsonSerializer.Serialize(writer, value, options);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "[SSR Hydration] Dropping {FieldName}: it cannot be written to the payload. "
                        + "The rest of the page's state is unaffected.", key);
                    stateDict.Remove(key);
                }
            }

            return stateDict;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read state for hydration");
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    /// The hydration payload: a field map PER COMPONENT, keyed the way the realizer named each one.
    ///
    /// <para>
    /// It used to be one flat map, because only the root could prefetch and only the root's fields
    /// travelled. Now that any component the page composes may declare server data, a flat map
    /// cannot say which component a field belongs to — two components holding a field of the same
    /// name would overwrite each other, silently and in whichever order reflection happened to
    /// return them.
    /// </para>
    /// </summary>
    private string? SerializeState(IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> byComponent)
    {
        if (byComponent.Count == 0) return null;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                byComponent, eQuantic.UI.Server.Json.EqJson.Options);
            _logger.LogDebug("[SSR Hydration] Serialized state for {Count} component(s)", byComponent.Count);
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize state for hydration");
            return null;
        }
    }
}
