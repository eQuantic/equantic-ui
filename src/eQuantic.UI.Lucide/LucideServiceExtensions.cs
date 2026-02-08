using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.Lucide;

public static class LucideServiceExtensions
{
    public static IServiceCollection AddLucideIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, LucideIconProvider>();
        return services;
    }
}
