using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.SimpleIcons;

public static class SimpleIconsServiceExtensions
{
    public static IServiceCollection AddSimpleIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, SimpleIconsIconProvider>();
        return services;
    }

    public static UIOptions UseSimpleIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddSimpleIcons());
        return options;
    }
}
