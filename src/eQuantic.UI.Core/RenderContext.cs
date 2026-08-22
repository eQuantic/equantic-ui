using System;
using System.Collections.Generic;
using System.Threading;

namespace eQuantic.UI.Core;

/// <summary>
/// Context available during component rendering
/// </summary>
public class RenderContext
{
    private readonly Dictionary<Type, object> _services = new();

    private static readonly AsyncLocal<IServiceProvider?> _asyncLocalProvider = new();
    private static IServiceProvider? _globalProvider;

    private static readonly AsyncLocal<RouteData?> _scopedRoute = new();
    private static readonly RouteData _emptyRoute = new();
    private RouteData? _route;

    /// <summary>
    /// Active route data — matched parameters + query string (e.g. <c>context.Route.Param("id")</c>).
    /// On the server it comes from the per-request scoped route (<see cref="SetScopedRoute"/>); never
    /// null. The compiler transpiles this to the runtime's <c>context.route</c>.
    /// </summary>
    public RouteData Route
    {
        get => _route ?? _scopedRoute.Value ?? _emptyRoute;
        set => _route = value;
    }

    /// <summary>
    /// Sets the route data for the current async context (SSR), thread-safe via AsyncLocal so concurrent
    /// requests don't interfere — mirroring <see cref="SetScopedServiceProvider"/>.
    /// </summary>
    public static void SetScopedRoute(RouteData? route) => _scopedRoute.Value = route;

    private static readonly AsyncLocal<Func<string, string>?> _linkPolicy = new();

    /// <summary>
    /// What an in-app destination becomes on the way into an href, for THIS render.
    /// <para>
    /// It exists for one policy — the language prefix — and it lives here rather than in the link
    /// node because the alternative is asking every author to remember it on every link. A site
    /// with two hundred hrefs has two hundred chances to forget, and forgetting is silent: the
    /// link works, it just leaves the language behind.
    /// </para>
    /// <para>
    /// AsyncLocal, like the route and the provider beside it: two requests rendering concurrently
    /// in different languages must not see each other's prefix.
    /// </para>
    /// </summary>
    public static Func<string, string>? LinkPolicy => _linkPolicy.Value;

    /// <inheritdoc cref="LinkPolicy"/>
    public static void SetLinkPolicy(Func<string, string>? policy) => _linkPolicy.Value = policy;

    /// <summary>The destination an href should carry — the policy's answer, or the destination
    /// untouched when no policy is in force. Only APP-INTERNAL rooted paths are offered to it:
    /// <c>//cdn</c>, <c>https://</c>, <c>#anchor</c> and <c>mailto:</c> belong to someone else.
    /// </summary>
    public static string ResolveDestination(string destination) =>
        _linkPolicy.Value is { } policy
        && destination.Length > 0
        && destination[0] == '/'
        && !destination.StartsWith("//", StringComparison.Ordinal)
            ? policy(destination)
            : destination;

    /// <summary>
    /// Service provider for the current async context.
    /// Uses AsyncLocal for thread-safe per-request isolation during SSR.
    /// Falls back to the global provider set at startup.
    /// </summary>
    public static IServiceProvider? ServiceProvider
    {
        get => _asyncLocalProvider.Value ?? _globalProvider;
        set
        {
            // If called from an async context (SSR), use AsyncLocal
            // Otherwise, set the global fallback
            if (SynchronizationContext.Current != null || _globalProvider != null)
            {
                _asyncLocalProvider.Value = value;
            }
            else
            {
                _globalProvider = value;
            }
        }
    }

    /// <summary>
    /// Sets the global (application-wide) service provider.
    /// Use this at startup (e.g., Program.cs). Thread-safe for reads.
    /// </summary>
    public static void SetGlobalServiceProvider(IServiceProvider? provider)
    {
        _globalProvider = provider;
    }

    /// <summary>
    /// Sets the service provider for the current async context only.
    /// Thread-safe: uses AsyncLocal so concurrent requests don't interfere.
    /// </summary>
    public static void SetScopedServiceProvider(IServiceProvider? provider)
    {
        _asyncLocalProvider.Value = provider;
    }

    /// <summary>
    /// Register a service for dependency injection
    /// </summary>
    public void RegisterService<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
    }
    
    /// <summary>
    /// Get a registered service
    /// </summary>
    public T GetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        // Fallback to global provider
        if (ServiceProvider != null)
        {
            var fallback = ServiceProvider.GetService(typeof(T)) as T;
            if (fallback != null) return fallback;
        }

        throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }
    
    /// <summary>
    /// Try to get a registered service
    /// </summary>
    public T? TryGetService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        
        // Fallback to global provider
        if (ServiceProvider != null)
        {
            return ServiceProvider.GetService(typeof(T)) as T;
        }

        return null;
    }
}
