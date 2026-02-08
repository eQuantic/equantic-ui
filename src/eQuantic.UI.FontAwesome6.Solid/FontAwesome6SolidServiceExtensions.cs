using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Solid;

public static class FontAwesome6SolidServiceExtensions
{
    public static IServiceCollection AddFontAwesome6SolidIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, FontAwesome6SolidIconProvider>();
        return services;
    }
}
