using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Brands;

public static class FontAwesome6BrandsServiceExtensions
{
    public static IServiceCollection AddFontAwesome6BrandsIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, FontAwesome6BrandsIconProvider>();
        return services;
    }
}
