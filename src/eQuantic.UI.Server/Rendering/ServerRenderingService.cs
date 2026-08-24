using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Assets;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Core.Rendering;
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
                               (typeof(StatelessComponent).IsAssignableFrom(t) ||
                                typeof(StatefulComponent).IsAssignableFrom(t) ||
                                // WRITE-ONCE pages (Photon vocabulary) — SSR bridges them through
                                // the web realizer (see CreateComponentInstance).
                                typeof(Primitives.UiComponent).IsAssignableFrom(t)));

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
            RenderContext.SetScopedServiceProvider(context.RequestServices);
            var routeData = BuildRouteData(context);
            RenderContext.SetScopedRoute(routeData);
            // The same route in a shape with no target in it — a write-once page reads
            // context.Route.Param("slug") instead of reaching for ASP.NET (and losing Photon).
            Primitives.RouteValues.Current = new Primitives.RouteValues(routeData.Params, routeData.QueryValues);
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
                // SERVER DATA FIRST (IServerPrefetch): load before the tree is built, with the REQUEST's
                // services, so the markup carries real values (crawlers see them) and the hydration
                // payload below hands the client the same ones.
                if (metadataSource is Primitives.IServerPrefetch prefetch)
                {
                    await prefetch.PrefetchAsync(context.RequestServices, context.RequestAborted);
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

                // For stateful components, we need to initialize state
                if (component is StatefulComponent stateful)
                {
                    // Initialize state synchronously for SSR
                    // The state's OnInit will be called
                    var state = stateful.GetType()
                        .GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)?
                        .GetValue(stateful);

                    // If there's an async initialization, we should await it
                    if (state is ComponentState cs)
                    {
                        // Call OnMount if it exists and is async
                        var onMountMethod = cs.GetType().GetMethod("OnMount");
                        if (onMountMethod != null)
                        {
                            var result = onMountMethod.Invoke(cs, null);
                            if (result is Task task)
                            {
                                await task;
                            }
                        }
                    }
                }

                // THE DRAWING, and everything that only the drawing needs. A client navigation asks
                // for none of it: the browser already has the component and builds the tree itself,
                // so markup rendered here would be markup thrown away — a whole tree build, plus its
                // assets and its atomic rules, per navigation.
                var assets = new AssetCollection();
                var html = string.Empty;
                // Declared out here for the same reason the html is: a page that did not draw has
                // no paint servers to declare, and the result is built past the end of this block.
                string? vectorDefs = null;
                if (draw)
                {
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
                    try
                    {
                        html = RenderComponent(component);
                    }
                    finally
                    {
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
                }

                // Serialize state for hydration. A WRITE-ONCE page carries its state in its OWN
                // fields (there is no separate state object), and the transpiled twin declares the
                // same field names — so the payload crosses by name, exactly like the Core path.
                string? serializedState = null;
                if (component is Web.VisualNodeComponent writeOnce && writeOnce.Node is Primitives.IServerPrefetch)
                {
                    serializedState = SerializeState(writeOnce.Node);
                }
                else if (component is StatefulComponent statefulComp)
                {
                    var state = statefulComp.GetType()
                        .GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)?
                        .GetValue(statefulComp);

                    if (state != null)
                    {
                        serializedState = SerializeState(state);
                    }
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
                RenderContext.SetScopedServiceProvider(null);
                RenderContext.SetScopedRoute(null);
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

        // For StatelessComponent, also walk Build() result to find nested components
        if (component is StatelessComponent stateless)
        {
            try
            {
                var built = stateless.Build(new RenderContext());
                CollectAssets(built, assets, services, visited);
            }
            catch
            {
                // Build may fail for components requiring DI - skip gracefully
            }
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
        // needs no counterpart: the transpiled page extends SharedStatefulComponent, which mounts/
        // hydrates directly (v1 fence: no server-driven initial state — field defaults render).
        if (instance is Primitives.UiComponent visual)
        {
            return new Web.VisualNodeComponent(visual, _options.Theme);
        }

        throw new InvalidOperationException($"Cannot create instance of component type: {componentType.Name}");
    }

    /// <summary>
    /// Builds the per-request <see cref="RouteData"/> from the HTTP route values and query string, so
    /// SSR sees the same parameters the client router will (e.g. <c>id</c> in <c>/users/{id}</c>).
    /// </summary>
    private static RouteData BuildRouteData(HttpContext context)
    {
        var routeParams = new Dictionary<string, string>();
        foreach (var rv in context.Request.RouteValues)
        {
            if (rv.Value is not null)
                routeParams[rv.Key] = rv.Value.ToString() ?? string.Empty;
        }

        var query = new Dictionary<string, string>();
        foreach (var q in context.Request.Query)
        {
            query[q.Key] = q.Value.ToString();
        }

        return new RouteData(routeParams, query);
    }

    /// <summary>
    /// Serializes component state to JSON for client-side hydration.
    /// </summary>
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

    private string? SerializeState(object state)
    {
        try
        {
            // Use reflection to extract all fields (including private) into a dictionary
            var stateDict = new Dictionary<string, object?>();
            var stateType = state.GetType();

            // Current implementation skips backing fields for auto-properties (they start with <)
            // We need to include them to preserve state during hydration
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var fields = state.GetType().GetFields(flags);
            
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

                // Skip null values and functions
                if (value != null && !value.GetType().IsSubclassOf(typeof(Delegate)))
                {
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
                    System.Text.Json.JsonSerializer.Serialize(value, options);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "[SSR Hydration] Dropping {FieldName}: it cannot be written to the payload. "
                        + "The rest of the page's state is unaffected.", key);
                    stateDict.Remove(key);
                }
            }

            var json = System.Text.Json.JsonSerializer.Serialize(stateDict, options);
            _logger.LogInformation($"[SSR Hydration] Serialized state: {json}");
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize state for hydration");
            return null;
        }
    }
}
