using Microsoft.Extensions.DependencyInjection;

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
}
