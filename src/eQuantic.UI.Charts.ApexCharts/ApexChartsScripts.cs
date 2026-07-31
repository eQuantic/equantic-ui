using eQuantic.UI.Core;

namespace eQuantic.UI.Charts.ApexCharts;

public class ApexChartsScripts : StatelessComponent
{
    public string Version { get; set; } = "3.45.1";

    public override IComponent Build(RenderContext context)
    {
        return new DynamicElement
        {
            TagName = "script",
            CustomAttributes = new Dictionary<string, string>
            {
                ["src"] = $"https://cdn.jsdelivr.net/npm/apexcharts@{Version}/dist/apexcharts.min.js",
                ["defer"] = "true"
            }
        };
    }
}
