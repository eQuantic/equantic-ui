using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Navigation;

public class TabItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = string.Empty;
    public HtmlElement? Content { get; set; }
    public bool Disabled { get; set; }
}

public class Tabs : HtmlElement
{
    public List<TabItem> Items { get; set; } = new();
    public string? ActiveTabId { get; set; }
    public bool Pills { get; set; }
    public bool Vertical { get; set; }
    public Action<string>? OnTabChange { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        var activeId = ActiveTabId ?? Items.FirstOrDefault()?.Id;
        
        // Nav tabs
        var navClasses = Pills ? "nav nav-pills" : "nav nav-tabs";
        if (Vertical) navClasses += " flex-column";
        
        var tabHeaders = Items.Select(tab =>
        {
            var linkClasses = "nav-link";
            if (tab.Id == activeId) linkClasses += " active";
            if (tab.Disabled) linkClasses += " disabled";

            return new HtmlNode
            {
                Tag = "li",
                Attributes = new Dictionary<string, string?> { ["class"] = "nav-item" },
                Children = {
                    new HtmlNode
                    {
                        Tag = "button",
                        Attributes = new Dictionary<string, string?>
                        {
                            ["class"] = linkClasses,
                            ["data-tab-id"] = tab.Id,
                            ["type"] = "button"
                        },
                        Children = { HtmlNode.Text(tab.Label) }
                    }
                }
            };
        }).ToList();

        var nav = new HtmlNode
        {
            Tag = "ul",
            Attributes = new Dictionary<string, string?> { ["class"] = navClasses, ["role"] = "tablist" },
            Children = tabHeaders
        };

        // Tab content
        var tabPanes = Items.Select(tab =>
        {
            var paneClasses = "tab-pane fade";
            if (tab.Id == activeId) paneClasses += " show active";

            return new HtmlNode
            {
                Tag = "div",
                Attributes = new Dictionary<string, string?>
                {
                    ["class"] = paneClasses,
                    ["id"] = $"tab-{tab.Id}"
                },
                Children = tab.Content != null ? new List<HtmlNode> { tab.Content.Render() } : new()
            };
        }).ToList();

        var content = new HtmlNode
        {
            Tag = "div",
            Attributes = new Dictionary<string, string?> { ["class"] = "tab-content" },
            Children = tabPanes
        };

        attrs["class"] = (attrs.GetValueOrDefault("class") + " tabs-container").Trim();

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = { nav, content }
        };
    }
}
