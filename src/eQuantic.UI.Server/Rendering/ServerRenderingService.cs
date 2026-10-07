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
    /// <summary>The pages this service renders, each by its TYPE. Keyed by the simple name, two pages of
    /// one name in two namespaces were one entry: the one registered last took it, and the other's
    /// route rendered it, with no error and no log line (#514).</summary>
    private readonly HashSet<Type> _pageTypes = new();

    public ServerRenderingService(
        IServiceProvider serviceProvider,
        UIOptions options,
        ILogger<ServerRenderingService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _isService = serviceProvider.GetService<IServiceProviderIsService>();

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
                    _pageTypes.Add(type);
                    _logger.LogDebug("Registered page type for SSR: {PageType}", type.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan assembly {Assembly} for page types", assembly.FullName);
            }
        }

        // Pages routed from Program.cs with MapPage<T> rather than a [Page] attribute, held as the
        // endpoint serves them, so everything downstream cannot tell the two ways of declaring a
        // route apart — which is the point.
        foreach (var (_, page, _) in _options.DeclaredRoutes)
        {
            _pageTypes.Add(page);
        }

        _logger.LogInformation("SSR initialized with {Count} page types", _pageTypes.Count);
    }

    /// <inheritdoc />
    public Task<ServerRenderResult> RenderPageAsync(Type pageType, HttpContext context) =>
        RunAsync(pageType, context, draw: true);

    /// <inheritdoc />
    public Task<ServerRenderResult> PreparePageAsync(Type pageType, HttpContext context) =>
        RunAsync(pageType, context, draw: false);

    /// <summary>
    /// The page's whole server-side moment, with the DRAWING optional.
    /// <para>
    /// Both callers need the same three things in the same order — prefetch, then metadata, then
    /// the status the page answers — from the request's own services. Only one of them needs the
    /// markup, and building a tree to throw it away is what made a client navigation cost as much
    /// as a full page.
    /// </para>
    /// </summary>
    private async Task<ServerRenderResult> RunAsync(Type pageType, HttpContext context, bool draw)
    {
        var pageTypeName = pageType.FullName ?? pageType.Name;
        if (!_pageTypes.Contains(pageType))
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
            // And the density the browser reported (#623), for the whole request: every write-once
            // bridge is built at it, the drawing's and the navigation's discovery walk alike, or a page
            // that composes by its density answers a navigation from a tree the browser never builds.
            Web.VisualNodeComponent.AmbientDensity = DensityCookie.Resolve(context);
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
                    coreRootKey = Web.ComponentIdentity.Key(metadataSource.GetType(), 0);
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
                        renderScope = new Web.ComponentExpansionScope
                        {
                            Restore = prefetched,
                            Reserved = coreRootKey,
                        };
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
                else
                {
                    // A navigation produces no markup, so there is no drawing to discover through —
                    // the walk has to expand on its own. It still costs only the pages that have
                    // something to find, because a navigation is answering with state in the first
                    // place.
                    //
                    // WHATEVER THE ROOT IS. This used to run for a write-once root alone, so an
                    // escape-hatch page composing a loader was asked on a full load and never on a
                    // navigation: the reader saw the number, clicked away, came back, and the second
                    // visit showed the default. The root's own kind decides how the ROOT is asked —
                    // directly, above — and says nothing about what it composes.
                    navigationPayload = await PrefetchTreeAsync(
                        component, prefetched, asked, context, coreRootKey);
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
                // AND every component holding a value from the container, prefetching or not: it
                // crosses as what the browser reads of it, or the browser draws the branch the server
                // did not.
                string? serializedState = null;
                var payload = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

                // A Core root is in no scope's Expanded — it never passes through the realizer's
                // component visit — but it IS the instance that drew: the pipeline renders that object
                // and never rebuilds it. Read here rather than beside the load, so what ships is the
                // state the markup left. Its ordinal is 0 by construction.
                if (metadataSource is not Primitives.UiComponent && (coreRoot is not null || CarriesProjection(metadataSource)))
                {
                    payload[coreRootKey ?? Web.ComponentIdentity.Key(metadataSource.GetType(), 0)] = Snapshot(metadataSource);
                }

                if (renderScope is not null)
                {
                    // OFF THE INSTANCE THAT DREW, or not at all. A component discovered in an earlier
                    // round but absent from the final tree has nothing on the page for its state to
                    // belong to, and shipping it would hand the client a key its own walk never reaches.
                    foreach (var (key, drew) in renderScope.Expanded)
                    {
                        if (asked.Contains(key) || CarriesProjection(drew)) payload[key] = Snapshot(drew);
                    }
                }
                else if (navigationPayload is not null)
                {
                    // A navigation draws nothing, so there is no rendered instance to read — the walk
                    // kept the wire form of each component as it went.
                    foreach (var (key, loaded) in navigationPayload) payload[key] = loaded;
                }

                if (payload.Count > 0)
                {
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
                Web.VisualNodeComponent.AmbientDensity = null;
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
        HttpContext context,
        string? reserved)
    {
        var wire = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);

        // The LAST round's tree, which is the one the client will build — the filter below reads it.
        Web.ComponentExpansionScope? settled = null;

        for (var round = 0; round < MaxPrefetchRounds; round++)
        {
            var scope = new Web.ComponentExpansionScope { Restore = loaded, Reserved = reserved };
            settled = scope;
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

        // THE TREE IT ENDED WITH, not every component a round passed through. A drawing reads its
        // payload off the instances that drew, so a component discovered in one round and gone from
        // the next is left out by construction; the walk here keeps what it loads as it goes, so it
        // has to drop the same ones itself or the two paths answer differently for one page. The
        // client cannot tell a stale key from a live one — it has no identity check of its own —
        // so it would hand that state to whatever landed on the name.
        if (settled is not null)
        {
            foreach (var stale in wire.Keys.Where(key => !settled.Expanded.ContainsKey(key)).ToList())
            {
                wire.Remove(stale);
            }

            // And a component holding a value from the container, which asked for nothing: what the
            // browser reads of it travels with the navigation as it does with the page.
            foreach (var (key, instance) in settled.Expanded)
            {
                if (!wire.ContainsKey(key) && CarriesProjection(instance)) wire[key] = Snapshot(instance);
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
    public bool IsSsrEnabled(Type pageType)
    {
        if (!_pageTypes.Contains(pageType))
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
    /// Component types whose missing or stale description has been logged, so a page served a thousand
    /// times says it once.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, byte> _reportedContracts = new();

    /// <summary>Members left out because they held a service, so a page served a thousand times says it once.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), byte> _reportedServices = new();

    /// <summary>What the container registered, which a value it hands out is never written whole for.</summary>
    private readonly IServiceProviderIsService? _isService;

    /// <summary>
    /// The type the container hands this one out as, or null: itself, or an interface it implements or a
    /// base it derives from, which is how a registration such as <c>AddSingleton&lt;IIdentity, SiteIdentity&gt;()</c>
    /// is keyed. Outside <c>System</c> only, where an app's contracts live: the container answers yes for
    /// <c>IEnumerable&lt;T&gt;</c> of anything, and every list would read as a service.
    /// </summary>
    private Type? ServiceContract(Type type)
    {
        if (_isService is null) return null;
        if (_isService.IsService(type)) return type;
        foreach (var contract in type.GetInterfaces())
            if (IsAppContract(contract) && _isService.IsService(contract)) return contract;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (IsAppContract(baseType) && _isService.IsService(baseType)) return baseType;
        return null;
    }

    private static bool IsAppContract(Type type) =>
        type.Namespace is not { } space || !(space == "System" || space.StartsWith("System.", StringComparison.Ordinal));

    /// <summary>
    /// Whether a component's state crosses although it asked for no data: a page holding a value from the
    /// container, which crosses as what the browser reads of it.
    /// </summary>
    private static bool CarriesProjection(object component) =>
        Rendering.HydrationContract.For(component.GetType()) is { HasProjection: true };

    /// <summary>
    /// ONE component's state as the wire carries it: in the page's payload and in the navigation payload.
    /// <para>
    /// WHAT THE BUILD DESCRIBED, AND NOTHING ELSE. The hydration manifest the source generator writes
    /// into the component's assembly says which members cross (<see cref="Rendering.HydrationContract"/>),
    /// and each goes out under the name the twin reads it by. This used to be decided here, by walking
    /// the fields: a member was named after the field the C# compiler synthesized and kept unless its
    /// type was an interface, so an auto-property and a public field in Pascal case crossed under a name
    /// the twin does not declare, and never landed.
    /// </para>
    /// </summary>
    private IReadOnlyDictionary<string, object?> Snapshot(object state)
    {
        try
        {
            var stateDict = new Dictionary<string, object?>();

            var type = state.GetType();
            var contract = Rendering.HydrationContract.For(type);
            if (contract is null)
            {
                if (_reportedContracts.TryAdd(type, 0))
                    _logger.LogWarning(
                        "[SSR Hydration] {Component} carries no state to the browser: its assembly's hydration "
                        + "manifest describes nothing of it. The SDK's source generator describes every component "
                        + "that prefetches and every page holding a value from the container, so either it holds "
                        + "nothing that crosses, or its assembly was built without the generator.", type.FullName);
                return stateDict;
            }

            if (contract.Unresolved.Count > 0 && _reportedContracts.TryAdd(type, 0))
                _logger.LogWarning(
                    "[SSR Hydration] {Component}'s hydration manifest names members it does not declare, which "
                    + "are left out: {Members}. Rebuild the assembly so its manifest is written again.",
                    type.FullName, string.Join(", ", contract.Unresolved));

            foreach (var hydrated in contract.Values)
            {
                var value = hydrated.Read(state);

                // A SERVER VALUE crosses as what the browser reads of it, which the build worked out
                // from the code the browser runs: whether it is null, and the members it reads.
                if (hydrated.Projection is { } projection)
                {
                    try
                    {
                        stateDict[hydrated.Name] = HydrationProjection.Of(value, projection, hydrated.Declared,
                            (read, foreign) =>
                            {
                                if (_reportedServices.TryAdd((type, $"{hydrated.Name}.{read}"), 0))
                                    _logger.LogWarning(
                                        "[SSR Hydration] {Component}.{Member}.{Read} holds {Foreign}: the browser's copy "
                                        + "would not find or order its elements as it does, so that read is left out of the "
                                        + "page and the rest of the value crosses. Build it with the default comparer, or "
                                        + "decide on the server and keep the result.",
                                        type.FullName, hydrated.Name, read, foreign);
                            });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[SSR Hydration] {Component}.{Member} could not be projected and is left out: {Message}",
                            type.FullName, hydrated.Name, ex.Message);
                    }
                    continue;
                }

                // A HANDLER NEVER TRAVELS, since the client builds its own. The manifest leaves out a
                // member DECLARED as a delegate; this catches one held behind a wider type.
                if (value is Delegate)
                {
                    continue;
                }

                // NOR A SERVICE, whatever the build said. The build follows the types it can see, and a
                // member typed `object` that holds a service at run time is the one shape it cannot: the
                // container knows what it registered, so it has the last word.
                if (value is not null && ServiceContract(value.GetType()) is { } service)
                {
                    if (_reportedServices.TryAdd((type, hydrated.Name), 0))
                        _logger.LogWarning(
                            "[SSR Hydration] {Component}.{Member} holds a {Value}, which the container hands out as "
                            + "the service {Service}, so it is left out of the page. A value the browser needs "
                            + "crosses as what it reads of it when the page declares it with that type.",
                            type.FullName, hydrated.Name, value.GetType().FullName, service.FullName);
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
                    stateDict[hydrated.Name] = null;
                    continue;
                }

                // Convert enums to lowercase string (matching JS compilation)
                if (value.GetType().IsEnum)
                {
                    var enumName = value.ToString() ?? "";
                    _logger.LogDebug("[SSR Enum] Converting enum {FieldName}: {Value} -> '{EnumName}'", hydrated.Name, value, enumName);

                    if (!string.IsNullOrEmpty(enumName))
                    {
                        var jsEnumValue = char.ToLowerInvariant(enumName[0]) + enumName.Substring(1);
                        stateDict[hydrated.Name] = jsEnumValue;
                    }
                    else
                    {
                        stateDict[hydrated.Name] = "0";
                    }
                }
                // A COLLECTION WITH ITS OWN COMPARER does not cross either: the wire carries its
                // elements, and the browser rebuilds it with the element type's default equality or
                // order, so a case-insensitive set would answer differently there.
                else if (ForeignComparer.Of(value, hydrated.Declared) is { } foreign)
                {
                    if (_reportedServices.TryAdd((type, hydrated.Name), 0))
                        _logger.LogWarning(
                            "[SSR Hydration] {Component}.{Member} holds {Foreign}: the browser's copy would not find "
                            + "or order its elements as it does, so it is left out of the page. Build it with the "
                            + "default comparer, or decide on the server and keep the result.",
                            type.FullName, hydrated.Name, foreign);
                }
                else
                {
                    stateDict[hydrated.Name] = value;
                }
            }

            // EqJson serializes Int64/UInt64 as strings so values beyond 2^53 survive into the
            // client BigInt-backed `long` runtime (matches the Server Action wire protocol).
            var options = eQuantic.UI.Server.Json.EqJson.Options;

            // FIELD BY FIELD, so one value that cannot be written does not take the rest with it: a
            // concrete type nobody thought of stays a missing field instead of an empty page, and
            // says so in the log rather than in silence.
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
