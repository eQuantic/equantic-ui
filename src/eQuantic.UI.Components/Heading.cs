using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Heading component - h1 to h6
/// </summary>
public class Heading : HtmlElement
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
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        return new HtmlNode
        {
            Tag = $"h{Math.Clamp(Level, 1, 6)}",
            Attributes = BuildAttributes(),
            Events = BuildEvents(),
            Children = new List<HtmlNode> { HtmlNode.Text(Content) }
        };
    }
}