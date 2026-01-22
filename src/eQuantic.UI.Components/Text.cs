using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Text component - renders as a span or paragraph
/// </summary>
public class Text : HtmlElement
{
    /// <summary>
    /// Text content
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to render as paragraph (p) instead of span
    /// </summary>
    public bool Paragraph { get; set; }
    
    /// <summary>
    /// Create a text component with content
    /// </summary>
    public Text() { }
    
    /// <summary>
    /// Create a text component with content
    /// </summary>
    public Text(string content)
    {
        Content = content;
    }
    
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        return new HtmlNode
        {
            Tag = Paragraph ? "p" : "span",
            Attributes = BuildAttributes(),
            Events = BuildEvents(),
            Children = new List<HtmlNode> { HtmlNode.Text(Content) }
        };
    }
}

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
