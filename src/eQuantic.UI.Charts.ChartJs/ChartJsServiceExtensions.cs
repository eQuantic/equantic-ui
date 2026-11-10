using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Charts.ChartJs;

public static class ChartJsServiceExtensions
{
    private const string DefaultVersion = "4.4.1";

    public static IServiceCollection AddChartJs(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Enables Chart.js integration by injecting CDN script and initialization logic into the HTML head.
    /// Must be called after AddUI() or via AddUI(options => options.UseChartJs()).
    /// </summary>
    public static WebApplication UseChartJs(this WebApplication app, string? version = null)
    {
        var options = app.Services.GetService<UIOptions>();

        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseChartJs().");
        }

        ConfigureChartJs(options, version);
        return app;
    }

    /// <summary>
    /// Enables Chart.js integration via UIOptions fluent API.
    /// </summary>
    public static UIOptions UseChartJs(this UIOptions options, string? version = null)
    {
        options.RegisterServices(services => services.AddChartJs());
        ConfigureChartJs(options, version);
        return options;
    }

    private static void ConfigureChartJs(UIOptions options, string? version = null)
    {
        var ver = version ?? DefaultVersion;
        var cdnTag = HtmlTag.Script($"https://cdn.jsdelivr.net/npm/chart.js@{ver}/dist/chart.umd.min.js");

        // Only add if not already present
        if (!options.HtmlShell.HeadTags.Any(t => t.Attributes.TryGetValue("src", out var src)
                && src is not null && (src.Contains("chart.js") || src.Contains("chart.umd"))))
        {
            options.HtmlShell.AddHeadTag(cdnTag);
        }

        // Add initialization script that discovers data-chart-init markers
        var initTag = HtmlTag.InlineScript(
            "document.addEventListener('DOMContentLoaded',function(){document.querySelectorAll('script[data-chart-init]').forEach(function(el){var id=el.getAttribute('data-chart-init');var cfg=JSON.parse(el.getAttribute('data-chart-config'));var c=document.getElementById(id);if(c&&typeof Chart!=='undefined')new Chart(c.getContext('2d'),cfg)})});");

        if (!options.HtmlShell.HeadTags.Any(t => t.Content?.Contains("data-chart-init") == true))
        {
            options.HtmlShell.AddHeadTag(initTag);
        }
    }
}
