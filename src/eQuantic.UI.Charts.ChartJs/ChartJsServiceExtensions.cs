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
    /// Must be called after AddUI().
    /// </summary>
    public static WebApplication UseChartJs(this WebApplication app, string? version = null)
    {
        var options = app.Services.GetService<UIOptions>();

        if (options == null)
        {
            throw new InvalidOperationException("UIOptions not found. Ensure AddUI() is called before UseChartJs().");
        }

        var ver = version ?? DefaultVersion;
        var cdnTag = $"<script src=\"https://cdn.jsdelivr.net/npm/chart.js@{ver}/dist/chart.umd.min.js\" defer></script>";

        // Only add if not already present
        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("chart.js") || t.Contains("chart.umd")))
        {
            options.HtmlShell.HeadTags.Add(cdnTag);
        }

        // Add initialization script that discovers data-chart-init markers
        var initTag = "<script defer>document.addEventListener('DOMContentLoaded',function(){document.querySelectorAll('script[data-chart-init]').forEach(function(el){var id=el.getAttribute('data-chart-init');var cfg=JSON.parse(el.getAttribute('data-chart-config'));var c=document.getElementById(id);if(c&&typeof Chart!=='undefined')new Chart(c.getContext('2d'),cfg)})});</script>";

        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("data-chart-init")))
        {
            options.HtmlShell.HeadTags.Add(initTag);
        }

        return app;
    }
}
