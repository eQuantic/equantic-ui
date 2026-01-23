using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Toggle switch component (wraps a checkbox)
/// </summary>
public class Switch : Checkbox
{
    public string? Label { get; set; }

    public override HtmlNode Render()
    {
        var checkboxNode = base.Render();
        checkboxNode.Attributes["class"] = (checkboxNode.Attributes.GetValueOrDefault("class") + " switch-input").Trim();
        
        // Structure: 
        // <label class="switch">
        //   <input type="checkbox" ...>
        //   <span class="slider round"></span>
        //   Text
        // </label>

        var children = new List<HtmlNode>
        {
            checkboxNode,
            new HtmlNode { Tag = "span", Attributes = new Dictionary<string, string?> { ["class"] = "slider" } }
        };

        if (Label != null)
        {
            children.Add(HtmlNode.Text(" " + Label));
        }

        return new HtmlNode
        {
            Tag = "label",
            Attributes = new Dictionary<string, string?> { ["class"] = "switch" },
            Children = children
        };
    }
}
