using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.FontAwesome6Regular;

public static class FontAwesome6RegularServiceExtensions
{
    public static IServiceCollection AddFontAwesome6RegularIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, FontAwesome6RegularIconProvider>();
        return services;
    }
}
