using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

public class Card : HtmlElement
{
    public HtmlElement? Header { get; set; }
    public HtmlElement? Footer { get; set; }
    public HtmlElement? Body { get; set; }
    public string? ImageUrl { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = (attrs.GetValueOrDefault("class") + " card").Trim();
        attrs["class"] = classes;

        var children = new List<HtmlNode>();

        if (ImageUrl != null)
        {
            children.Add(new HtmlNode
            {
                Tag = "img",
                Attributes = new Dictionary<string, string?> 
                { 
                    ["src"] = ImageUrl,
                    ["class"] = "card-img-top",
                    ["alt"] = "Card image"
                }
            });
        }

        if (Header != null)
        {
            var headerNode = Header.Render();
            headerNode.Attributes["class"] = (headerNode.Attributes.GetValueOrDefault("class") + " card-header").Trim();
            children.Add(headerNode);
        }

        if (Body != null)
        {
            var bodyNode = Body.Render();
            bodyNode.Attributes["class"] = (bodyNode.Attributes.GetValueOrDefault("class") + " card-body").Trim();
            children.Add(bodyNode);
        }
        else if (Children.Any())
        {
            // If explicit children are used instead of Body property
             children.Add(new HtmlNode
             {
                 Tag = "div",
                 Attributes = new Dictionary<string, string?> { ["class"] = "card-body" },
                 Children = Children.Select(c => c.Render()).ToList()
             });
        }

        if (Footer != null)
        {
            var footerNode = Footer.Render();
            footerNode.Attributes["class"] = (footerNode.Attributes.GetValueOrDefault("class") + " card-footer").Trim();
            children.Add(footerNode);
        }

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}
