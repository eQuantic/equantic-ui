using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;
using eQuantic.UI.Core;

namespace eQuantic.UI.Phosphor;

public static class PhosphorServiceExtensions
{
    public static IServiceCollection AddPhosphorIcons(this IServiceCollection services)
    {
        services.AddSingleton<IIconProvider, PhosphorIconProvider>();
        return services;
    }

    public static UIOptions UsePhosphorIcons(this UIOptions options)
    {
        options.RegisterServices(services => services.AddPhosphorIcons());
        return options;
    }
}
