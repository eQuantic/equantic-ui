using System.Web;

namespace eQuantic.UI.Core.Assets;

/// <summary>
/// Represents an asset dependency (script, stylesheet, etc.) required by a component.
/// </summary>
public interface IAsset
{
    /// <summary>
    /// Unique key for deduplication. Assets with the same key are considered identical.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Optional HTML id for client-side manipulation.
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// Renders the asset as an HTML tag (e.g., script or link tag).
    /// </summary>
    string Render();
}

/// <summary>
/// An external script loaded via src attribute.
/// </summary>
public record ScriptAsset(string Src, bool Defer = true, string? Id = null) : IAsset
{
    public string Key => $"script:{Src}";
    public string Render()
    {
        var src = HttpUtility.HtmlAttributeEncode(Src);
        var idAttr = !string.IsNullOrWhiteSpace(Id) ? $" id=\"{HttpUtility.HtmlAttributeEncode(Id)}\"" : "";
        return Defer
            ? $"<script src=\"{src}\" defer{idAttr}></script>"
            : $"<script src=\"{src}\"{idAttr}></script>";
    }
}

/// <summary>
/// An inline script block.
/// </summary>
public record InlineScriptAsset(string Content, string? Id = null) : IAsset
{
    public string Key => $"inline-script:{Content.GetHashCode()}";
    public string Render()
    {
        var idAttr = !string.IsNullOrWhiteSpace(Id) ? $" id=\"{HttpUtility.HtmlAttributeEncode(Id)}\"" : "";
        return $"<script{idAttr}>{Content}</script>";
    }
}

/// <summary>
/// An external stylesheet loaded via link tag.
/// </summary>
public record StylesheetAsset(string Href, string? Id = null) : IAsset
{
    public string Key => $"stylesheet:{Href}";
    public string Render()
    {
        var href = HttpUtility.HtmlAttributeEncode(Href);
        var idAttr = !string.IsNullOrWhiteSpace(Id) ? $" id=\"{HttpUtility.HtmlAttributeEncode(Id)}\"" : "";
        return $"<link rel=\"stylesheet\" href=\"{href}\"{idAttr}>";
    }
}

/// <summary>
/// An inline style block.
/// </summary>
public record InlineStyleAsset(string Content, string? Id = null) : IAsset
{
    public string Key => $"inline-style:{Content.GetHashCode()}";
    public string Render()
    {
        var idAttr = !string.IsNullOrWhiteSpace(Id) ? $" id=\"{HttpUtility.HtmlAttributeEncode(Id)}\"" : "";
        return $"<style{idAttr}>{Content}</style>";
    }
}
