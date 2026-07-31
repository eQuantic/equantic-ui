using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Charts.ApexCharts.Models;

namespace eQuantic.UI.Charts.ApexCharts;

public class ApexChart<T> : StatelessComponent, IChart
{
    public string? Title { get; set; }
    public bool Responsive { get; set; } = true;
    public ApexOptions Options { get; set; } = new();
    public List<ApexSeries<T>> Series { get; set; } = new();

    public override IComponent Build(RenderContext context)
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
