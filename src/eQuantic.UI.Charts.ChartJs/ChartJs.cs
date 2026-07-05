using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;
using eQuantic.UI.Charts.ChartJs.Models;

namespace eQuantic.UI.Charts.ChartJs;

public class ChartJs<T> : StatelessComponent, IChart
{
    public string? Title { get; set; }
    public bool Responsive { get; set; } = true;
    public string Type { get; set; } = "line";
    public ChartData<T> Data { get; set; } = new();
    public ChartJsOptions Options { get; set; } = new();
    public string? Width { get; set; } = "100%";
    public string? Height { get; set; } = "400px";

    public override IComponent Build(RenderContext context)
    {
        var id = Id ?? $"chart-{Guid.NewGuid():N}";
        
        // Container for the canvas
        var container = new DynamicElement
        {
            TagName = "div",
            ClassName = "chart-container",
            Style = new HtmlStyle
            {
                Position = Position.Relative,
                Width = Width,
                Height = Height
            },
            Children = 
            {
                new DynamicElement
                {
                    TagName = "canvas",
                    Id = id
                }
            }
        };

        // Initialize script - this is where the interop happens
        // Note: In eQuantic.UI, we can use a custom script delivery or a specialized component
        // For now, we'll embed a script tag or use a pattern that the runtime understands.
        var initScript = new DynamicElement
        {
            TagName = "script",
            // We use a specific attribute to tell the client-side runtime to execute this after mount
            DataAttributes = new Dictionary<string, string>
            {
                ["chart-init"] = id,
                ["chart-config"] = System.Text.Json.JsonSerializer.Serialize(new 
                {
                    type = Type,
                    data = Data,
                    options = Options
                })
            }
        };
        
        container.Children.Add(initScript);

        return container;
    }
}
