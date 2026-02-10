using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.Heroicons;

public static class HeroiconsServiceExtensions
{
    public static IServiceCollection AddHeroIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, HeroiconsIconProvider>();
        return services;
    }

    public static UIOptions UseHeroIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddHeroIcons());
        return options;
    }
}
