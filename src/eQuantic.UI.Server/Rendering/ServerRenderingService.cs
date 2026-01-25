using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using eQuantic.UI.Core;
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
                    .Where(t => t.GetCustomAttribute<PageAttribute>() != null &&
                               !t.IsAbstract &&
                               (typeof(StatelessComponent).IsAssignableFrom(t) ||
                                typeof(StatefulComponent).IsAssignableFrom(t)));

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
            var pageAttr = pageType.GetCustomAttribute<PageAttribute>();
            if (pageAttr?.DisableSsr == true)
            {
                _logger.LogDebug("SSR disabled for page: {PageType}", pageTypeName);
                return ServerRenderResult.NotAvailable();
            }

            // Create the component instance with DI
            var component = CreateComponentInstance(pageType);

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

            // Render the component to HTML
            var html = RenderComponent(component);

            _logger.LogDebug("SSR completed for page: {PageType}, HTML length: {Length}",
                pageTypeName, html.Length);

            return ServerRenderResult.Ok(html);
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

        var pageAttr = pageType.GetCustomAttribute<PageAttribute>();
        return pageAttr?.DisableSsr != true;
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

        throw new InvalidOperationException($"Cannot create instance of component type: {componentType.Name}");
    }
}
