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
