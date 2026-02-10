using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.BootstrapIcons;

public static class BootstrapIconsServiceExtensions
{
    public static IServiceCollection AddBootstrapIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, BootstrapIconsIconProvider>();
        return services;
    }

    public static UIOptions UseBootstrapIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddBootstrapIcons());
        return options;
    }
}
