using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Brands;

public static class FontAwesome6BrandsServiceExtensions
{
    public static IServiceCollection AddFontAwesome6BrandsIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, FontAwesome6BrandsIconProvider>();
        return services;
    }

    public static UIOptions UseFontAwesome6BrandsIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddFontAwesome6BrandsIcons());
        return options;
    }
}
