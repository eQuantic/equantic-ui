using eQuantic.UI.Core;

namespace TodoListApp.Components;

public class Checkbox : HtmlElement
{
    public bool Checked { get; set; }
    public Action<bool>? OnChange { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["type"] = "checkbox";
        if (Checked) attrs["checked"] = "true";

        var events = BuildEvents();
        
        // Map change event (string) to bool callback
        if (OnChange != null)
        {
            events["change"] = (string val) => OnChange(val == "true" || val == "on");
        }

        return new HtmlNode
        {
            Tag = "input",
            Attributes = attrs,
            Events = events
        };
    }
}
