using eQuantic.UI.Primitives;
using eQuantic.UI.Server;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The server half of <see cref="IThemeController"/>. It exists so that nothing has to GUESS the
/// mode during SSR — a guess there is not cosmetic, it decides the markup the browser paints first.
/// </summary>
public class SsrThemeControllerTests
{
    [Fact]
    public void AddUI_ResolvesAThemeController()
    {
        var services = new ServiceCollection();
        services.AddUI();

        var controller = services.BuildServiceProvider().GetService<IThemeController>();

        controller.Should().NotBeNull("a toggle that resolves nothing during SSR has to guess");
    }

    [Fact]
    public void WithoutAChoice_TheModeIsLight()
    {
        var services = new ServiceCollection();
        services.AddUI();

        services.BuildServiceProvider().GetRequiredService<IThemeController>()
            .Mode.Should().Be(ThemeMode.Light);
    }

    [Fact]
    public void TheAppCanDeclareTheMode()
    {
        var services = new ServiceCollection();
        services.AddUI(options => options.UseInitialThemeMode(ThemeMode.Dark));

        services.BuildServiceProvider().GetRequiredService<IThemeController>()
            .Mode.Should().Be(ThemeMode.Dark);
    }

    /// <summary>
    /// Applying on the server must not stick. This is a SINGLETON: remembering one request's toggle
    /// would hand that visitor's choice to the next visitor's first paint.
    /// </summary>
    [Fact]
    public void ApplyingOnTheServer_DoesNotLeakBetweenRequests()
    {
        var controller = new SsrThemeController(ThemeMode.Light);

        controller.Apply(ThemeMode.Dark);

        controller.Mode.Should().Be(ThemeMode.Light);
    }

    /// <summary>An app that brings its own — one that reads a cookie, say — keeps it.</summary>
    [Fact]
    public void AnAppsOwnController_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThemeController>(new SsrThemeController(ThemeMode.Dark));
        services.AddUI(options => options.UseInitialThemeMode(ThemeMode.Light));

        services.BuildServiceProvider().GetRequiredService<IThemeController>()
            .Mode.Should().Be(ThemeMode.Dark);
    }
}
