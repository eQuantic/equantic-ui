using System.Collections.Generic;
using eQuantic.UI.Web;
using eQuantic.UI.Charts.ApexCharts.Models;

namespace eQuantic.UI.Charts.ApexCharts;

public class ApexChart<T> : HtmlElement, IChart
{
    // Escape hatch (CLAUDE.md §Styling): this wraps a third-party JS library, so it emits raw
    // markup instead of the write-once vocabulary. It builds an HtmlElement DIRECTLY — the
    // Core component bases it used to derive from were the pre-write-once model and are gone.
    public string? Title { get; set; }
    public bool Responsive { get; set; } = true;
    public ApexOptions Options { get; set; } = new();
    public List<ApexSeries<T>> Series { get; set; } = new();

    public override HtmlNode Render() => BuildElement().Render();

    private IComponent BuildElement()
    {
        var id = Id ?? $"apex-chart-{Guid.NewGuid():N}";
        
        var container = new DynamicElement
        {
            TagName = "div",
            Id = id,
            ClassName = "apex-chart-container"
        };

        var initScript = new DynamicElement
        {
            TagName = "script",
            DataAttributes = new Dictionary<string, string>
            {
                ["apex-init"] = id,
                ["apex-config"] = System.Text.Json.JsonSerializer.Serialize(new 
                {
                    series = Series,
                    chart = Options.Chart,
                    stroke = Options.Stroke,
                    xaxis = Options.XAxis,
                    yaxis = Options.YAxis,
                    legend = Options.Legend,
                    dataLabels = Options.DataLabels,
                    title = Options.Title,
                    labels = Options.Labels,
                    colors = Options.Colors
                })
            }
        };
        
        container.Children.Add(initScript);

        return container;
    }
}
