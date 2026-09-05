using System.Web;

namespace eQuantic.UI.Server.Metadata;

public class NameMetaTag : MetaTag
{
    public string Name { get; }
    public string Content { get; }

    public override string Key => $"name:{Name}";

    public NameMetaTag(string name, string content)
    {
        Name = name;
        Content = content;
    }

    public override string Render() => $"<meta name=\"{HttpUtility.HtmlAttributeEncode(Name)}\" content=\"{HttpUtility.HtmlAttributeEncode(Content)}\">";
}

public class PropertyMetaTag : MetaTag
{
    public string Property { get; }
    public string Content { get; }

    public override string Key => $"property:{Property}";

    public PropertyMetaTag(string property, string content)
    {
        Property = property;
        Content = content;
    }

    public override string Render() => $"<meta property=\"{HttpUtility.HtmlAttributeEncode(Property)}\" content=\"{HttpUtility.HtmlAttributeEncode(Content)}\">";
}

public class LinkTag : MetaTag
{
    public string Rel { get; }
    public string Href { get; }
    public string? Type { get; }

    /// <summary>
    /// The language this link points AT — <c>hreflang</c>, the attribute that turns a set of
    /// <c>rel="alternate"</c> links into a translation group a search engine can read. Null for
    /// every other kind of link, which is most of them.
    /// </summary>
    public string? Hreflang { get; }

    /// <summary>
    /// What makes this tag the same tag. A page has ONE canonical, so <c>rel</c> alone identifies
    /// it; it has one alternate PER LANGUAGE, so an alternate is identified by both. Keying every
    /// link by rel alone is what collapsed a translation group into a single link, which is the
    /// one shape of this feature that is worse than not having it: a group of one says the page
    /// exists in no other language.
    /// </summary>
    public override string Key => Hreflang is null ? $"link:{Rel}" : $"link:{Rel}:{Hreflang}";

    public LinkTag(string rel, string href, string? type = null, string? hreflang = null)
    {
        Rel = rel;
        Href = href;
        Type = type;
        Hreflang = hreflang;
    }

    public override string Render()
    {
        var typeAttr = string.IsNullOrEmpty(Type) ? "" : $" type=\"{HttpUtility.HtmlAttributeEncode(Type)}\"";
        var langAttr = string.IsNullOrEmpty(Hreflang)
            ? ""
            : $" hreflang=\"{HttpUtility.HtmlAttributeEncode(Hreflang)}\"";
        return $"<link rel=\"{HttpUtility.HtmlAttributeEncode(Rel)}\"{langAttr} "
            + $"href=\"{HttpUtility.HtmlAttributeEncode(Href)}\"{typeAttr}>";
    }
}
