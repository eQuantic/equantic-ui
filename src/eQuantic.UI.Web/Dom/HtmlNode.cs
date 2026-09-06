using System;
using System.Collections.Generic;

namespace eQuantic.UI.Web;

/// <summary>
/// Represents a virtual DOM node for rendering
/// </summary>
public class HtmlNode
{
    /// <summary>
    /// Reconciliation identity among siblings — the client's keyed diff moves an element whose key
    /// moved instead of rewriting it in place. Never an attribute: SSR emits nothing for it.
    /// Settable because the realizer's funnel (<c>WebRealizer.LowerNode</c>) stamps it on the
    /// element after the kind-specific lowering built it, the way it stamps the bookmark id.
    /// </summary>
    public string? Key { get; set; }

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
