using System.Text;

namespace eQuantic.UI.Server;

/// <summary>
/// A head element as DATA — tag name, attributes and optional content — instead of a hand-typed
/// HTML string. App code never quotes, escapes or closes anything: the shell renders the tag, and
/// attribute values are HTML-encoded on the way out (a title or description carrying <c>"</c> or
/// <c>&amp;</c> can't break out of its attribute).
/// <para>
/// Void elements (<c>meta</c>, <c>link</c>, <c>base</c>) render without a closing tag; everything
/// else wraps <see cref="Content"/>. Content is written VERBATIM — it is the script/style body,
/// which is exactly what must not be encoded — so only pass content you author.
/// </para>
/// </summary>
public sealed class HtmlTag
{
    /// <summary>Elements that never take a closing tag (the HTML void set, head subset).</summary>
    private static readonly HashSet<string> VoidElements =
        new(StringComparer.OrdinalIgnoreCase) { "meta", "link", "base", "br", "hr", "img", "input", "source" };

    public HtmlTag(string tagName)
    {
        TagName = tagName;
    }

    public string TagName { get; init; }

    /// <summary>Attribute name → value. A <c>null</c> value renders as a BOOLEAN attribute
    /// (<c>crossorigin</c>, <c>defer</c>) — the name alone, no <c>="…"</c>.</summary>
    public Dictionary<string, string?> Attributes { get; init; } = new();

    /// <summary>Inner content for non-void elements (a script or style body). Written verbatim.</summary>
    public string? Content { get; init; }

    /// <summary>Fluent attribute setter — <c>new HtmlTag("link").Attr("rel", "stylesheet")</c>.</summary>
    public HtmlTag Attr(string name, string? value = null)
    {
        Attributes[name] = value;
        return this;
    }

    /// <summary>A <c>&lt;meta name="…" content="…"&gt;</c>.</summary>
    public static HtmlTag Meta(string name, string content) =>
        new("meta") { Attributes = { ["name"] = name, ["content"] = content } };

    /// <summary>A <c>&lt;link&gt;</c> with the given <c>rel</c> and <c>href</c>.</summary>
    public static HtmlTag Link(string rel, string href) =>
        new("link") { Attributes = { ["rel"] = rel, ["href"] = href } };

    /// <summary>An external <c>&lt;script src&gt;</c> (deferred by default — head scripts must not
    /// block parsing).</summary>
    public static HtmlTag Script(string src, bool defer = true)
    {
        var tag = new HtmlTag("script") { Attributes = { ["src"] = src } };
        if (defer) tag.Attributes["defer"] = null;
        return tag;
    }

    /// <summary>An INLINE <c>&lt;script&gt;</c> — the body is written verbatim.</summary>
    public static HtmlTag InlineScript(string body, bool defer = true)
    {
        var tag = new HtmlTag("script") { Content = body };
        if (defer) tag.Attributes["defer"] = null;
        return tag;
    }

    /// <summary>The rendered element. Attribute VALUES are HTML-encoded; <see cref="Content"/> is not
    /// (it is a script/style body by construction).</summary>
    public string Render()
    {
        var html = new StringBuilder("<").Append(TagName);
        foreach (var (name, value) in Attributes)
        {
            html.Append(' ').Append(name);
            if (value != null)
                html.Append("=\"").Append(System.Net.WebUtility.HtmlEncode(value)).Append('"');
        }
        html.Append('>');
        if (VoidElements.Contains(TagName)) return html.ToString();
        return html.Append(Content).Append("</").Append(TagName).Append('>').ToString();
    }

    public override string ToString() => Render();
}
