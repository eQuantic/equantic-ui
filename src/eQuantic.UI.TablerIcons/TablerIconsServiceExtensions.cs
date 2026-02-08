using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.TablerIcons;

public static class TablerIconsServiceExtensions
{
    public static IServiceCollection AddTablerIconsIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, TablerIconsIconProvider>();
        return services;
    }
}
