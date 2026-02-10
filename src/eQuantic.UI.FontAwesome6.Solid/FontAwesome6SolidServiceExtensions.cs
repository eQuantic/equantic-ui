using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Solid;

public static class FontAwesome6SolidServiceExtensions
{
    public static IServiceCollection AddFontAwesome6SolidIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, FontAwesome6SolidIconProvider>();
        return services;
    }

    public static UIOptions UseFontAwesome6SolidIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddFontAwesome6SolidIcons());
        return options;
    }
}
