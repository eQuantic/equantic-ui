using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.RadixIcons;

public static class RadixIconsServiceExtensions
{
    public static IServiceCollection AddRadixIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, RadixIconsIconProvider>();
        return services;
    }

    public static UIOptions UseRadixIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddRadixIcons());
        return options;
    }
}
