using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.Iconoir;

public static class IconoirServiceExtensions
{
    public static IServiceCollection AddIconoirIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, IconoirIconProvider>();
        return services;
    }

    public static UIOptions UseIconoirIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddIconoirIcons());
        return options;
    }
}
