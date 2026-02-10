using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.TablerIcons;

public static class TablerIconsServiceExtensions
{
    public static IServiceCollection AddTablerIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, TablerIconsIconProvider>();
        return services;
    }

    public static UIOptions UseTablerIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddTablerIcons());
        return options;
    }
}
