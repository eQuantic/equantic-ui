using eQuantic.UI.Core;
using eQuantic.UI.Web.Components;

namespace eQuantic.UI.Charts.ChartJs;

public class ChartJsScripts : StatelessComponent
{
    public string Version { get; set; } = "4.4.1";

    public override IComponent Build(RenderContext context)
    {
        return new DynamicElement
        {
            TagName = "script",
            CustomAttributes = new Dictionary<string, string>
            {
                ["src"] = $"https://cdn.jsdelivr.net/npm/chart.js@{Version}/dist/chart.umd.min.js",
                ["defer"] = "true"
            }
        };
    }
}
