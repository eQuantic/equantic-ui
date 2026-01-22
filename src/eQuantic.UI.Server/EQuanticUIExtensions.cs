using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Extension methods for configuring eQuantic.UI in ASP.NET Core.
/// </summary>
public static class EQuanticUIExtensions
{
    /// <summary>
    /// Adds eQuantic.UI services to the DI container.
    /// </summary>
    public static IServiceCollection AddEQuanticUI(this IServiceCollection services, Action<EQuanticUIOptions>? configure = null)
    {
        var options = new EQuanticUIOptions();
        configure?.Invoke(options);
        
        services.AddSingleton(options);
        services.AddSingleton<IServerActionRegistry>(sp =>
        {
            var registry = new ServerActionRegistry();
            
            // Scan registered assemblies
            foreach (var assembly in options.AssembliesToScan)
            {
                registry.ScanAssembly(assembly);
            }
            
            return registry;
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds the Server Actions middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseEQuanticServerActions(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ServerActionsMiddleware>();
    }
    
    /// <summary>
    /// Maps eQuantic.UI pages (placeholder for future routing).
    /// </summary>
    public static IEndpointRouteBuilder MapEQuanticPages(this IEndpointRouteBuilder endpoints)
    {
        // TODO: Implement page routing based on [Page] attributes
        // For now, this is a placeholder that will be expanded
        return endpoints;
    }
}

/// <summary>
/// Configuration options for eQuantic.UI.
/// </summary>
public class EQuanticUIOptions
{
    internal List<Assembly> AssembliesToScan { get; } = new();
    
    /// <summary>
    /// Scan an assembly for components with [Page] and [ServerAction] attributes.
    /// </summary>
    public EQuanticUIOptions ScanAssembly(Assembly assembly)
    {
        AssembliesToScan.Add(assembly);
        return this;
    }
}
