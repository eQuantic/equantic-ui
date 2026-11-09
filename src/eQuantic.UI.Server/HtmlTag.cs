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
/// <para>
/// A <c>script</c>, a <c>style</c> or a stylesheet <c>link</c> is rendered with the request's nonce, which the shell hands
/// <see cref="Render"/>: an element the shell writes is one a Content-Security-Policy can admit by
/// that nonce, and an app never writes the attribute itself (#1266).
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
    /// (it is a script/style body by construction). A <c>script</c> or a <c>style</c> carries
    /// <paramref name="nonce"/> when one is given; any other element ignores it.</summary>
    public string Render(string? nonce)
    {
        var html = new StringBuilder("<").Append(TagName);
        foreach (var (name, value) in Attributes)
        {
            html.Append(' ').Append(name);
            if (value != null)
                html.Append("=\"").Append(System.Net.WebUtility.HtmlEncode(value)).Append('"');
        }
        if (CarriesNonce()) html.Append(ShellNonce.Attribute(nonce));
        html.Append('>');
        if (VoidElements.Contains(TagName)) return html.ToString();
        return html.Append(Content).Append("</").Append(TagName).Append('>').ToString();
    }

    public override string ToString() => Render(nonce: null);

    /// <summary>
    /// The elements a Content-Security-Policy admits by nonce: scripts, styles, and a stylesheet
    /// link, which <c>style-src</c> governs as it governs a style element (#1268).
    /// </summary>
    private bool CarriesNonce() =>
        TagName.Equals("script", StringComparison.OrdinalIgnoreCase)
        || TagName.Equals("style", StringComparison.OrdinalIgnoreCase)
        || (TagName.Equals("link", StringComparison.OrdinalIgnoreCase)
            && Attributes.TryGetValue("rel", out var rel) && rel is not null
            && rel.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("stylesheet", StringComparer.OrdinalIgnoreCase));
}
