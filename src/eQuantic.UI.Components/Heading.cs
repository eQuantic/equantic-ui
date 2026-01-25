using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Heading component - h1 to h6
/// </summary>
public class Heading : StatelessComponent
{
    /// <summary>
    /// Heading level (1-6)
    /// </summary>
    public int Level { get; set; } = 1;
    
    /// <summary>
    /// Heading text
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    public Heading() { }
    
    public Heading(string content, int level = 1)
    {
        Content = content;
        Level = Math.Clamp(level, 1, 6);
    }
    
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var textTheme = theme?.Typography;
        
        var baseStyle = "";
        if (textTheme != null) baseStyle = textTheme.Base;
        
        var variantStyle = "";
        var variantKey = "h" + this.Level;
        if (textTheme != null && textTheme.Variants != null)
        {
             var v = textTheme.Variants[variantKey];
             if (v != null) variantStyle = v;
        }

        var attrs = new Dictionary<string, string>
        {
            ["class"] = baseStyle + " " + variantStyle + " " + (this.ClassName != null ? this.ClassName : "")
        };

        return new DynamicElement
        {
            TagName = "h" + this.Level,
            InnerText = this.Content,
            CustomAttributes = attrs,
            CustomEvents = BuildEvents()
        };
    }
}