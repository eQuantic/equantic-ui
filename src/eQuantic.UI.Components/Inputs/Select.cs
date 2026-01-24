using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Select : InputComponent<string>
{
    public string? Name { get; set; }
    public bool Multiple { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public List<SelectOption> Options { get; set; } = new();

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        if (Name != null) attrs["name"] = Name;
        if (Multiple) attrs["multiple"] = "true";
        if (Disabled) attrs["disabled"] = "true";
        if (Required) attrs["required"] = "true";
        
        // Handle change event
        var events = BuildEvents();
        if (OnChange != null)
        {
            // For selects, wrapping the change event to extract value
            // implementation depends on how runtime handles it.
            // Using standard change event for now.
             // Note: The Runtime's Bind logic usually handles this, 
             // but specific OnChange<string> might need compiler support or runtime bridge.
             // For now, we map it to standard "change" event and let compiler/runtime handle value extraction.
             // The compiler usually generates: (e) => OnChange(e.target.value)
        }

        var children = new List<HtmlNode>();
        foreach (var opt in Options)
        {
            var optAttrs = new Dictionary<string, string?>
            {
                ["value"] = opt.Value
            };
            
            if (opt.Disabled) optAttrs["disabled"] = "true";
            
            // Check if selected matches Value (single) or if opt.Selected is explicitly true
            bool isSelected = opt.Selected;
            if (!Multiple && Value != null && opt.Value == Value)
            {
                isSelected = true;
            }
            
            if (isSelected) optAttrs["selected"] = "selected";

            children.Add(new HtmlNode
            {
                Tag = "option",
                Attributes = optAttrs,
                Children =  { HtmlNode.Text(opt.Label) }
            });
        }

        return new HtmlNode
        {
            Tag = "select",
            Attributes = attrs,
            Events = events,
            Children = children
        };
    }
}
