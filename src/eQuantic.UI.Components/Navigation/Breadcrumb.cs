using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation;

public class BreadcrumbItem
{
    public string Label { get; set; } = string.Empty;
    public string? Href { get; set; }
    public bool Active { get; set; }
}

public class Breadcrumb : HtmlElement
{
    public List<BreadcrumbItem> Items { get; set; } = new();
    public string Separator { get; set; } = "/";

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["aria-label"] = "breadcrumb";

        var items = Items.Select((item, index) =>
        {
            var isLast = index == Items.Count - 1;
            var classes = "breadcrumb-item";
            if (isLast || item.Active) classes += " active";

            HtmlNode content;
            if (isLast || item.Active || item.Href == null)
            {
                content = HtmlNode.Text(item.Label);
            }
            else
            {
                content = new HtmlNode
                {
                    Tag = "a",
                    Attributes = new Dictionary<string, string?> { ["href"] = item.Href },
                    Children = { HtmlNode.Text(item.Label) }
                };
            }

            return new HtmlNode
            {
                Tag = "li",
                Attributes = new Dictionary<string, string?> { ["class"] = classes },
                Children = { content }
            };
        }).ToList();

        return new HtmlNode
        {
            Tag = "nav",
            Attributes = attrs,
            Children = {
                new HtmlNode
                {
                    Tag = "ol",
                    Attributes = new Dictionary<string, string?>
                    {
                        ["class"] = "breadcrumb",
                        ["style"] = $"--bs-breadcrumb-divider: '{Separator}';"
                    },
                    Children = items
                }
            }
        };
    }
}
