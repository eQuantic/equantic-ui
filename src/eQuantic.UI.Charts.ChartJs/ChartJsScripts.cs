using eQuantic.UI.Core;

namespace eQuantic.UI.Charts.ChartJs;

public class ChartJsScripts : HtmlElement
{
    // Escape hatch (CLAUDE.md §Styling): this wraps a third-party JS library, so it emits raw
    // markup instead of the write-once vocabulary. It builds an HtmlElement DIRECTLY — the
    // Core component bases it used to derive from were the pre-write-once model and are gone.
    public string Version { get; set; } = "4.4.1";

    public override HtmlNode Render() => BuildElement().Render();

    private IComponent BuildElement()
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
