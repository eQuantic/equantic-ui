using eQuantic.UI.Core.Build;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Registry for discovering and managing style providers.
/// Style providers are discovered via assembly scanning.
/// </summary>
public class StyleProviderRegistry
{
    private readonly List<IStyleProvider> _providers = new();
    
    /// <summary>
    /// Discovers and registers all style providers from loaded assemblies.
    /// </summary>
    public void DiscoverProviders()
    {
        var providerType = typeof(IStyleProvider);
        
        var providers = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => providerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t =>
            {
                try
                {
                    return Activator.CreateInstance(t) as IStyleProvider;
                }
                catch
                {
                    return null;
                }
            })
            .Where(p => p != null)
            .Cast<IStyleProvider>();
            
        foreach (var provider in providers)
        {
            if (!_providers.Any(p => p.FrameworkId == provider.FrameworkId))
            {
                _providers.Add(provider);
            }
        }
    }
    
    /// <summary>
    /// Manually registers a style provider.
    /// </summary>
    public void Register(IStyleProvider provider)
    {
        if (!_providers.Any(p => p.FrameworkId == provider.FrameworkId))
        {
            _providers.Add(provider);
        }
    }
    
    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IReadOnlyList<IStyleProvider> Providers => _providers;
    
    /// <summary>
    /// Gets a provider by framework ID.
    /// </summary>
    public IStyleProvider? GetProvider(string frameworkId)
        => _providers.FirstOrDefault(p => p.FrameworkId == frameworkId);
}
