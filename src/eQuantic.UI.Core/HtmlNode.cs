using System;
using System.Collections.Generic;

namespace eQuantic.UI.Core;

/// <summary>
/// Represents a virtual DOM node for rendering
/// </summary>
public class HtmlNode
{
    /// <summary>
    /// HTML tag name (div, button, input, etc.)
    /// </summary>
    public required string Tag { get; init; }

    /// <summary>
    /// HTML attributes
    /// </summary>
    public Dictionary<string, string?> Attributes { get; init; } = new();

    /// <summary>
    /// Event handlers
    /// </summary>
    public Dictionary<string, Delegate> Events { get; init; } = new();

    /// <summary>
    /// Child nodes
    /// </summary>
    public List<HtmlNode> Children { get; init; } = new();

    /// <summary>
    /// Text content (for text nodes)
    /// </summary>
    public string? TextContent { get; init; }

    /// <summary>
    /// Creates a text-only node
    /// </summary>
    public static HtmlNode Text(string content) => new()
    {
        Tag = "#text",
        TextContent = content
    };
}
