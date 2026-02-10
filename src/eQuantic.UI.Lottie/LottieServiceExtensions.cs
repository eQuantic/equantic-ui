using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Lottie;

public static class LottieServiceExtensions
{
    /// <summary>
    /// Adds Lottie animation services to the service collection.
    /// </summary>
    public static IServiceCollection AddLottie(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Enables Lottie animation support via UIOptions fluent API.
    /// </summary>
    public static UIOptions UseLottie(this UIOptions options)
    {
        options.RegisterServices(services => services.AddLottie());
        return options;
    }
}
