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
        var cdnTag = $"<script src=\"https://cdn.jsdelivr.net/npm/apexcharts@{ver}/dist/apexcharts.min.js\" defer></script>";

        // Only add if not already present
        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("apexcharts")))
        {
            options.HtmlShell.HeadTags.Add(cdnTag);
        }

        // Add initialization script that discovers data-apex-init markers
        var initTag = "<script defer>document.addEventListener('DOMContentLoaded',function(){document.querySelectorAll('script[data-apex-init]').forEach(function(el){var id=el.getAttribute('data-apex-init');var cfg=JSON.parse(el.getAttribute('data-apex-config'));var c=document.getElementById(id);if(c&&typeof ApexCharts!=='undefined')new ApexCharts(c,cfg).render()})});</script>";

        if (!options.HtmlShell.HeadTags.Any(t => t.Contains("data-apex-init")))
        {
            options.HtmlShell.HeadTags.Add(initTag);
        }
    }
}
