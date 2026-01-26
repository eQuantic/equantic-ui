using System.Web;

namespace eQuantic.UI.Core.Metadata;

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

    public override string Key => $"link:{Rel}";

    public LinkTag(string rel, string href, string? type = null)
    {
        Rel = rel;
        Href = href;
        Type = type;
    }

    public override string Render() 
    {
        var typeAttr = string.IsNullOrEmpty(Type) ? "" : $" type=\"{HttpUtility.HtmlAttributeEncode(Type)}\"";
        return $"<link rel=\"{HttpUtility.HtmlAttributeEncode(Rel)}\" href=\"{HttpUtility.HtmlAttributeEncode(Href)}\"{typeAttr}>";
    }
}
