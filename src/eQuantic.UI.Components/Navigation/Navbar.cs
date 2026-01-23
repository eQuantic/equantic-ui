using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation;

public class NavItem
{
    public string Label { get; set; } = string.Empty;
    public string? Href { get; set; }
    public bool Active { get; set; }
    public bool Disabled { get; set; }
    public Action? OnClick { get; set; }
    public List<NavItem> SubItems { get; set; } = new();
}

public class Navbar : HtmlElement
{
    public string? Brand { get; set; }
    public string? BrandHref { get; set; } = "/";
    public List<NavItem> Items { get; set; } = new();
    public bool Dark { get; set; }
    public bool Fixed { get; set; }
    public bool Sticky { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        var classes = "navbar navbar-expand-lg";
        classes += Dark ? " navbar-dark bg-dark" : " navbar-light bg-light";
        if (Fixed) classes += " fixed-top";
        if (Sticky) classes += " sticky-top";
        
        attrs["class"] = (attrs.GetValueOrDefault("class") + " " + classes).Trim();

        var navChildren = new List<HtmlNode>();

        // Brand
        if (Brand != null)
        {
            navChildren.Add(new HtmlNode
            {
                Tag = "a",
                Attributes = new Dictionary<string, string?>
                {
                    ["class"] = "navbar-brand",
                    ["href"] = BrandHref
                },
                Children = { HtmlNode.Text(Brand) }
            });
        }

        // Toggler for mobile
        navChildren.Add(new HtmlNode
        {
            Tag = "button",
            Attributes = new Dictionary<string, string?>
            {
                ["class"] = "navbar-toggler",
                ["type"] = "button",
                ["data-bs-toggle"] = "collapse",
                ["data-bs-target"] = "#navbarNav",
                ["aria-controls"] = "navbarNav",
                ["aria-expanded"] = "false",
                ["aria-label"] = "Toggle navigation"
            },
            Children = {
                new HtmlNode
                {
                    Tag = "span",
                    Attributes = new Dictionary<string, string?> { ["class"] = "navbar-toggler-icon" }
                }
            }
        });

        // Nav items
        var navItems = Items.Select(item =>
        {
            var itemClasses = "nav-link";
            if (item.Active) itemClasses += " active";
            if (item.Disabled) itemClasses += " disabled";

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
                            ["class"] = itemClasses,
                            ["href"] = item.Href ?? "#"
                        },
                        Children = { HtmlNode.Text(item.Label) }
                    }
                }
            };
        }).ToList();

        var collapse = new HtmlNode
        {
            Tag = "div",
            Attributes = new Dictionary<string, string?>
            {
                ["class"] = "collapse navbar-collapse",
                ["id"] = "navbarNav"
            },
            Children = {
                new HtmlNode
                {
                    Tag = "ul",
                    Attributes = new Dictionary<string, string?> { ["class"] = "navbar-nav" },
                    Children = navItems
                }
            }
        };

        navChildren.Add(collapse);

        // Container
        var container = new HtmlNode
        {
            Tag = "div",
            Attributes = new Dictionary<string, string?> { ["class"] = "container-fluid" },
            Children = navChildren
        };

        return new HtmlNode
        {
            Tag = "nav",
            Attributes = attrs,
            Children = { container }
        };
    }
}
