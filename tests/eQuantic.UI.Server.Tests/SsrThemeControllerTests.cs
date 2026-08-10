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
        var controller = new SsrThemeController(declared: ThemeMode.Light);

        controller.Apply(ThemeMode.Dark);

        controller.Mode.Should().Be(ThemeMode.Light);
    }

    /// <summary>
    /// The visitor's remembered choice beats the app's default. This is what removes the flash: the
    /// FIRST byte already carries the mode they picked, instead of the default arriving and
    /// hydration correcting it in front of them.
    /// </summary>
    [Theory]
    [InlineData("dark", ThemeMode.Dark)]
    [InlineData("light", ThemeMode.Light)]
    public void ARememberedChoice_BeatsTheDeclaredDefault(string cookie, ThemeMode expected)
    {
        var controller = new SsrThemeController(Requesting(cookie), ThemeMode.Light);

        controller.Mode.Should().Be(expected);
    }

    /// <summary>A cookie is user-supplied text. The only two answers this question has are light and
    /// dark; anything else is ignored rather than trusted.</summary>
    [Theory]
    [InlineData("chartreuse")]
    [InlineData("")]
    public void AnUnrecognisedCookie_FallsBackToTheDeclaredDefault(string cookie)
    {
        new SsrThemeController(Requesting(cookie), ThemeMode.Dark)
            .Mode.Should().Be(ThemeMode.Dark);
    }

    /// <summary>
    /// Read per request, never captured. The controller is a SINGLETON, so a mode captured at
    /// construction would hand one visitor's choice to the next visitor's first paint.
    /// </summary>
    [Fact]
    public void TheModeIsReadPerRequest_NotCapturedOnce()
    {
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor();
        var controller = new SsrThemeController(accessor, ThemeMode.Light);

        accessor.HttpContext = Context("dark");
        controller.Mode.Should().Be(ThemeMode.Dark);

        // The next visitor, through the same singleton.
        accessor.HttpContext = Context(null);
        controller.Mode.Should().Be(ThemeMode.Light, "one visitor's choice must not survive into another's");
    }

    private static Microsoft.AspNetCore.Http.HttpContext Context(string? cookie)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        if (cookie != null)
            context.Request.Headers.Cookie = $"eq-theme={cookie}";
        return context;
    }

    private static Microsoft.AspNetCore.Http.IHttpContextAccessor Requesting(string? cookie) =>
        new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = Context(cookie) };

    /// <summary>An app that brings its own — one that reads a header, say — keeps it.</summary>
    [Fact]
    public void AnAppsOwnController_Wins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThemeController>(new SsrThemeController(declared: ThemeMode.Dark));
        services.AddUI(options => options.UseInitialThemeMode(ThemeMode.Light));

        services.BuildServiceProvider().GetRequiredService<IThemeController>()
            .Mode.Should().Be(ThemeMode.Dark);
    }
}
