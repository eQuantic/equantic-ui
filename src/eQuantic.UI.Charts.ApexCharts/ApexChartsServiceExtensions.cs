using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Server;

namespace eQuantic.UI.Charts.ApexCharts;

public static class ApexChartsServiceExtensions
{
    private const string DefaultVersion = "3.45.1";

    public static IServiceCollection AddApexCharts(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Enables ApexCharts integration by injecting CDN script and initialization logic into the HTML head.
    /// Must be called after AddUI() or via AddUI(options => options.UseApexCharts()).
    /// </summary>
    public static WebApplication UseApexCharts(this WebApplication app, string? version = null)
    {
        var options = app.Services.GetService<UIOptions>();

        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseApexCharts().");
        }

        ConfigureApexCharts(options, version);
        return app;
    }

    /// <summary>
    /// Enables ApexCharts integration via UIOptions fluent API.
    /// </summary>
    public static UIOptions UseApexCharts(this UIOptions options, string? version = null)
    {
        options.RegisterServices(services => services.AddApexCharts());
        ConfigureApexCharts(options, version);
        return options;
    }

    private static void ConfigureApexCharts(UIOptions options, string? version = null)
    {
        var ver = version ?? DefaultVersion;
        var cdnTag = HtmlTag.Script($"https://cdn.jsdelivr.net/npm/apexcharts@{ver}/dist/apexcharts.min.js");

        // Only add if not already present
        if (!options.HtmlShell.HeadTags.Any(t => t.Attributes.TryGetValue("src", out var src)
                && src is not null && src.Contains("apexcharts")))
        {
            options.HtmlShell.AddHeadTag(cdnTag);
        }

        // Add initialization script that discovers data-apex-init markers. ApexCharts writes its own CSS
        // in a style element it creates, which a strict policy refuses unless the chart is handed the
        // page's nonce (`chart.nonce`), so the initializer hands it the one the shell stamped (#1268).
        var initTag = HtmlTag.InlineScript(
            "document.addEventListener('DOMContentLoaded',function(){document.querySelectorAll('script[data-apex-init]').forEach(function(el){var id=el.getAttribute('data-apex-init');var cfg=JSON.parse(el.getAttribute('data-apex-config'));var n=document.querySelector('script[nonce],style[nonce]');var nonce=n&&(n.nonce||n.getAttribute('nonce'));if(nonce){cfg.chart=cfg.chart||{};cfg.chart.nonce=nonce;}var c=document.getElementById(id);if(c&&typeof ApexCharts!=='undefined')new ApexCharts(c,cfg).render()})});");

        if (!options.HtmlShell.HeadTags.Any(t => t.Content?.Contains("data-apex-init") == true))
        {
            options.HtmlShell.AddHeadTag(initTag);
        }
    }
}
