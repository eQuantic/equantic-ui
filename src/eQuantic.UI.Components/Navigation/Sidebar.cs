using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation;

public class SidebarItem
{
    public string Label { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Href { get; set; }
    public bool Active { get; set; }
    public List<SidebarItem> SubItems { get; set; } = new();
    public Action? OnClick { get; set; }
}

public class Sidebar : HtmlElement
{
    public List<SidebarItem> Items { get; set; } = new();
    public string? Width { get; set; } = "250px";
    public bool Collapsed { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = "sidebar d-flex flex-column";
        if (Collapsed) classes += " collapsed";
        
        attrs["class"] = (attrs.GetValueOrDefault("class") + " " + classes).Trim();
        
        var style = $"width: {(Collapsed ? "60px" : Width)}; min-height: 100vh;";
        attrs["style"] = (attrs.GetValueOrDefault("style") + "; " + style).Trim(';', ' ');

        var navItems = Items.Select(RenderItem).ToList();

        return new HtmlNode
        {
            Tag = "nav",
            Attributes = attrs,
            Children = {
                new HtmlNode
                {
                    Tag = "ul",
                    Attributes = new Dictionary<string, string?> { ["class"] = "nav nav-pills flex-column mb-auto" },
                    Children = navItems
                }
            }
        };
    }

    private HtmlNode RenderItem(SidebarItem item)
    {
        var linkClasses = "nav-link";
        if (item.Active) linkClasses += " active";

        var children = new List<HtmlNode>();

        if (item.Icon != null)
        {
            children.Add(new HtmlNode
            {
                Tag = "i",
                Attributes = new Dictionary<string, string?> { ["class"] = item.Icon }
            });
        }

        if (!Collapsed)
        {
            children.Add(HtmlNode.Text(" " + item.Label));
        }

        return new HtmlNode
        {
            Tag = "li",
            Attributes = new Dictionary<string, string?> { ["class"] = "nav-item" },
            Children = {
                new HtmlNode
                {
                    Tag = "a",
                    Attributes = new Dictionary<string, string?>
                    {
                        ["class"] = linkClasses,
                        ["href"] = item.Href ?? "#"
                    },
                    Children = children
                }
            }
        };
    }
}
