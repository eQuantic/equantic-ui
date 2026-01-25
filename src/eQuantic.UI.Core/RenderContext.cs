using System;
using System.Collections.Generic;

namespace eQuantic.UI.Core;

/// <summary>
/// Context available during component rendering
/// </summary>
public class RenderContext
{
    private readonly Dictionary<Type, object> _services = new();
    
    /// <summary>
    /// Global Service Provider fallback for manually created components
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

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
