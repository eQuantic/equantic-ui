using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Regular;

public static class FontAwesome6RegularServiceExtensions
{
    public static IServiceCollection AddFontAwesome6RegularIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, FontAwesome6RegularIconProvider>();
        return services;
    }

    public static UIOptions UseFontAwesome6RegularIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddFontAwesome6RegularIcons());
        return options;
    }
}
