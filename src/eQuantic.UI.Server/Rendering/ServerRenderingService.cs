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

        _logger.LogInformation("SSR initialized with {Count} page types", _pageTypes.Count);
    }

    /// <inheritdoc />
    public async Task<ServerRenderResult> RenderPageAsync(string pageTypeName, HttpContext context)
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
            var component = CreateComponentInstance(pageType);

            // Handle Metadata (for write-once pages the real page instance sits inside the bridge)
            MetadataCollection? metadata = null;
            object metadataSource = component is Web.VisualNodeComponent bridge ? bridge.Node : component;
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

            // Set scoped provider + route data for this async context (thread-safe via AsyncLocal)
            RenderContext.SetScopedServiceProvider(context.RequestServices);
            RenderContext.SetScopedRoute(BuildRouteData(context));

            try
            {
                // Collect asset dependencies before rendering
                var assets = new AssetCollection();
                CollectAssets(component, assets, context.RequestServices, new HashSet<Type>());

                // Render the component to HTML
                var html = RenderComponent(component);

                // Serialize state for hydration (if component is stateful)
                string? serializedState = null;
                if (component is StatefulComponent statefulComp)
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

                return ServerRenderResult.Ok(html, metadata, serializedState, assets.HasAssets ? assets : null);
            }
            finally
            {
                RenderContext.SetScopedServiceProvider(null);
                RenderContext.SetScopedRoute(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSR failed for page: {PageType}", pageTypeName);
            return ServerRenderResult.Fail(ex.Message);
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

    private IComponent CreateComponentInstance(Type componentType)
    {
        // Try to create using DI first
        try
        {
            var component = ActivatorUtilities.CreateInstance(_serviceProvider, componentType);
            if (component is IComponent ic)
            {
                return ic;
            }
            if (component is Primitives.UiComponent visualFromDi)
            {
                return new Web.VisualNodeComponent(visualFromDi);
            }
        }
        catch
        {
            // Fallback to parameterless constructor
        }

        // Try parameterless constructor
        var instance = Activator.CreateInstance(componentType);
        if (instance is IComponent component2)
        {
            return component2;
        }

        // A WRITE-ONCE page (eQuantic.UI.Primitives.UiComponent) is not an IComponent — bridge it
        // through the web-realizer adapter so the SSR pipeline stays IComponent-only. The client
        // needs no counterpart: the transpiled page extends SharedStatefulComponent, which mounts/
        // hydrates directly (v1 fence: no server-driven initial state — field defaults render).
        if (instance is Primitives.UiComponent visual)
        {
            return new Web.VisualNodeComponent(visual);
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
