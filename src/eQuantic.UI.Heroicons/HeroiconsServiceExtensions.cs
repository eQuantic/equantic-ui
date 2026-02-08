using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.Heroicons;

public static class HeroiconsServiceExtensions
{
    public static IServiceCollection AddHeroiconsIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, HeroiconsIconProvider>();
        return services;
    }
}
