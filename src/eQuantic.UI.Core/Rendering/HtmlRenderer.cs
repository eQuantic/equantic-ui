using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace eQuantic.UI.Core.Rendering;

/// <summary>
/// Renders HtmlNode virtual DOM trees to HTML strings.
/// Used for Server-Side Rendering (SSR) to generate SEO-friendly HTML.
/// </summary>
/// <remarks>
/// This renderer converts C# components directly to HTML without going through
/// the TypeScript compilation pipeline, enabling fast server-side rendering
/// for initial page loads and SEO optimization.
/// </remarks>
public static class HtmlRenderer
{
    /// <summary>
    /// Self-closing HTML tags that don't require a closing tag.
    /// </summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    /// <summary>
    /// Renders a component to an HTML string.
    /// </summary>
    /// <param name="component">The component to render.</param>
    /// <returns>The HTML string representation of the component.</returns>
    public static string RenderToString(IComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var node = component.Render();
        return RenderNode(node);
    }

    /// <summary>
    /// Renders an HtmlNode tree to an HTML string.
    /// </summary>
    /// <param name="node">The root node to render.</param>
    /// <returns>The HTML string representation.</returns>
    public static string RenderNode(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var sb = new StringBuilder();
        RenderNodeInternal(node, sb);
        return sb.ToString();
    }

    /// <summary>
    /// Renders an HtmlNode tree to an HTML string with formatting for readability.
    /// </summary>
    /// <param name="node">The root node to render.</param>
    /// <param name="indent">Initial indentation level.</param>
    /// <returns>The formatted HTML string representation.</returns>
    public static string RenderNodePretty(HtmlNode node, int indent = 0)
    {
        ArgumentNullException.ThrowIfNull(node);

        var sb = new StringBuilder();
        RenderNodePrettyInternal(node, sb, indent);
        return sb.ToString();
    }

    private static void RenderNodeInternal(HtmlNode node, StringBuilder sb)
    {
        // Handle text nodes
        if (node.Tag == "#text")
        {
            if (node.TextContent != null)
            {
                sb.Append(HttpUtility.HtmlEncode(node.TextContent));
            }
            return;
        }

        // Handle raw HTML (for dangerously set innerHTML - use with caution)
        if (node.Tag == "#raw")
        {
            sb.Append(node.TextContent ?? "");
            return;
        }

        // Handle fragments (no wrapper element)
        if (node.Tag == "#fragment")
        {
            foreach (var child in node.Children)
            {
                RenderNodeInternal(child, sb);
            }
            return;
        }

        // Start tag
        sb.Append('<');
        sb.Append(node.Tag);

        // Render attributes
        RenderAttributes(node.Attributes, sb);

        // Check for void elements
        if (VoidElements.Contains(node.Tag))
        {
            sb.Append(" />");
            return;
        }

        sb.Append('>');

        // Render text content if present
        if (node.TextContent != null)
        {
            sb.Append(HttpUtility.HtmlEncode(node.TextContent));
        }

        // Render children
        foreach (var child in node.Children)
        {
            RenderNodeInternal(child, sb);
        }

        // End tag
        sb.Append("</");
        sb.Append(node.Tag);
        sb.Append('>');
    }

    private static void RenderNodePrettyInternal(HtmlNode node, StringBuilder sb, int indent)
    {
        var indentStr = new string(' ', indent * 2);

        // Handle text nodes
        if (node.Tag == "#text")
        {
            if (!string.IsNullOrWhiteSpace(node.TextContent))
            {
                sb.Append(indentStr);
                sb.AppendLine(HttpUtility.HtmlEncode(node.TextContent.Trim()));
            }
            return;
        }

        // Handle raw HTML
        if (node.Tag == "#raw")
        {
            sb.Append(indentStr);
            sb.AppendLine(node.TextContent ?? "");
            return;
        }

        // Handle fragments
        if (node.Tag == "#fragment")
        {
            foreach (var child in node.Children)
            {
                RenderNodePrettyInternal(child, sb, indent);
            }
            return;
        }

        // Start tag
        sb.Append(indentStr);
        sb.Append('<');
        sb.Append(node.Tag);

        // Render attributes
        RenderAttributes(node.Attributes, sb);

        // Void elements
        if (VoidElements.Contains(node.Tag))
        {
            sb.AppendLine(" />");
            return;
        }

        // Check if this is an inline element (text content only, no children)
        var isInline = node.Children.Count == 0 && node.TextContent != null;

        if (isInline)
        {
            sb.Append('>');
            sb.Append(HttpUtility.HtmlEncode(node.TextContent));
            sb.Append("</");
            sb.Append(node.Tag);
            sb.AppendLine(">");
            return;
        }

        sb.AppendLine(">");

        // Render text content if present
        if (node.TextContent != null)
        {
            sb.Append(new string(' ', (indent + 1) * 2));
            sb.AppendLine(HttpUtility.HtmlEncode(node.TextContent));
        }

        // Render children
        foreach (var child in node.Children)
        {
            RenderNodePrettyInternal(child, sb, indent + 1);
        }

        // End tag
        sb.Append(indentStr);
        sb.Append("</");
        sb.Append(node.Tag);
        sb.AppendLine(">");
    }

    private static void RenderAttributes(Dictionary<string, string?> attributes, StringBuilder sb)
    {
        foreach (var (key, value) in attributes)
        {
            // Skip null values
            if (value == null) continue;

            // Skip event handlers (they are client-side only)
            if (key.StartsWith("on", StringComparison.OrdinalIgnoreCase) &&
                key.Length > 2 && char.IsUpper(key[2]))
            {
                continue;
            }

            sb.Append(' ');
            sb.Append(key);

            // Boolean attributes (hidden, disabled, etc.)
            if (value == "true" || value == key)
            {
                continue;
            }

            sb.Append("=\"");
            sb.Append(HttpUtility.HtmlAttributeEncode(value));
            sb.Append('"');
        }
    }
}
