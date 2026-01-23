using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class TextArea : HtmlElement
{
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public int Rows { get; set; } = 3;
    public bool Disabled { get; set; }
    
    public Action<string>? OnChange { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Disabled) attrs["disabled"] = "true";
        attrs["rows"] = Rows.ToString();
        
        var children = new List<HtmlNode>();
        if (Value != null)
        {
            children.Add(HtmlNode.Text(Value));
        }

        return new HtmlNode
        {
            Tag = "textarea",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}
