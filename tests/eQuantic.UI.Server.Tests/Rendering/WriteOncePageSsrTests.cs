using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Server.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using StatefulComponent = eQuantic.UI.Primitives.StatefulComponent;

namespace eQuantic.UI.Server.Tests.Rendering;

/// <summary>
/// SSR of WRITE-ONCE pages (unification slice 3): a Primitives StatefulComponent with [Page] is a
/// full page — the scan registers it and the render bridges it through the web realizer, producing
/// token-styled HTML with the component's initial state (v1 fence: field defaults, no server-driven
/// initial state).
/// </summary>
public class WriteOncePageSsrTests
{
    [Page("/write-once-test", Title = "Write-once test page")]
    private sealed class WriteOnceTestPage : StatefulComponent
    {
        private int _count;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text($"Count: {_count}", TypeRole.Heading));
            return new Primitives.Box(new BoxStyle
            {
                Padding = EdgeInsets.All(Space.S4),
                Background = context.Theme.Surface,
            }, column);
        }
    }

    private static ServerRenderingService CreateService(IAppTheme? theme = null)
    {
        var options = new UIOptions();
        options.ScanAssembly(typeof(WriteOncePageSsrTests).Assembly);
        if (theme != null) options.UseTheme(theme);
        return new ServerRenderingService(
            new ServiceCollection().BuildServiceProvider(),
            options,
            NullLogger<ServerRenderingService>.Instance);
    }

    [Fact]
    public async Task RenderPageAsync_RendersAWriteOncePage_WithTokensAndInitialState()
    {
        var service = CreateService();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("Count: 0", "field defaults are the v1 initial state");
        // Mode-free token output — the same light-dark() the client lowering hydrates against.
        result.Html.Should().Contain("light-dark(#ffffff, #14181e)", "Surface token from the theme");
        result.Html.Should().Contain("box-sizing: border-box");
    }

    [Fact]
    public void RenderComponent_BridgesAnAbstractTree_ThroughTheWebRealizer()
    {
        var service = CreateService();
        var html = service.RenderComponent(
            new Web.VisualNodeComponent(new Primitives.Text("hello", TypeRole.Caption)));

        html.Should().Contain("eq-type-caption");
        html.Should().Contain("hello");
    }

    [Fact]
    public async Task RenderPageAsync_HonorsTheAppSelectedTheme_LoweringTheSamePageWithMaterialTokens()
    {
        // options.UseTheme(Material) → the SSR pipeline lowers the SAME write-once page with Material's
        // tokens: the M3 baseline Surface (#f7f2fa / #1d1b20), NOT Photon's (#ffffff / #14181e). This is
        // the server half of the bridge — the client half applies the matching __EQ_THEME__ at boot.
        var service = CreateService(Material.MaterialTheme.Instance);
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = await service.RenderPageAsync(nameof(WriteOnceTestPage), context);

        result.Success.Should().BeTrue(result.Error);
        result.Html.Should().Contain("light-dark(#f7f2fa, #1d1b20)", "Material's M3 Surface token");
        result.Html.Should().NotContain("light-dark(#ffffff, #14181e)", "the Photon Surface must be gone");
    }
}
