using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Core;

namespace eQuantic.UI.RadixIcons;

public static class RadixIconsServiceExtensions
{
    public static IServiceCollection AddRadixIconsIcons(this IServiceCollection services)
    {
        services.AddTransient<IIconProvider, RadixIconsIconProvider>();
        return services;
    }
}
