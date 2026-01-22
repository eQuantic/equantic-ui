namespace eQuantic.UI.Core;

/// <summary>
/// Context available during component rendering
/// </summary>
public class RenderContext
{
    private readonly Dictionary<Type, object> _services = new();
    
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
        return null;
    }
}
