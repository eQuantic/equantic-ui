using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// A scrollable area with custom-styled scrollbars.
/// Augments native scrolling with visual scrollbar indicators.
///
/// Usage:
/// <code>
/// new ScrollArea {
///     MaxHeight = "300px",
///     Children = {
///         // ... long content ...
///     }
/// }
/// </code>
/// </summary>
public class ScrollArea : StatelessComponent
{
    /// <summary>Maximum height of the scroll area. Default: "300px".</summary>
    public string MaxHeight { get; set; } = "300px";

    /// <summary>Maximum width of the scroll area (for horizontal scroll). Default: null.</summary>
    public string? MaxWidth { get; set; }

    /// <summary>
    /// Scrollbar orientation. Default: Vertical.
    /// Set to Horizontal for horizontal scrolling, or Both for bidirectional.
    /// </summary>
    public ScrollOrientation ScrollOrientation { get; set; } = ScrollOrientation.Vertical;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var scrollTheme = theme?.ScrollArea;

        var rootClass = scrollTheme?.Root ?? "eq-scroll-area";
        var viewportClass = scrollTheme?.Viewport ?? "eq-scroll-viewport";
        var scrollbarClass = scrollTheme?.Scrollbar ?? "eq-scrollbar";
        var thumbClass = scrollTheme?.Thumb ?? "eq-scrollbar-thumb";

        // Build style
        var styles = new List<string>();
        if (ScrollOrientation == ScrollOrientation.Vertical || ScrollOrientation == ScrollOrientation.Both)
            styles.Add($"max-height:{MaxHeight}");
        if (MaxWidth != null && (ScrollOrientation == ScrollOrientation.Horizontal || ScrollOrientation == ScrollOrientation.Both))
            styles.Add($"max-width:{MaxWidth}");

        var overflowStyle = ScrollOrientation switch
        {
            ScrollOrientation.Horizontal => "overflow-x:auto;overflow-y:hidden",
            ScrollOrientation.Both => "overflow:auto",
            _ => "overflow-y:auto;overflow-x:hidden"
        };
        styles.Add(overflowStyle);

        var root = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{rootClass} {ClassName}".Trim(),
                ["data-orientation"] = ScrollOrientation.ToString().ToLowerInvariant()
            }
        };

        // Viewport
        var viewport = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = viewportClass,
                ["style"] = string.Join(";", styles),
                ["tabindex"] = "0"
            }
        };

        foreach (var child in Children) viewport.Children.Add(child);

        root.Children.Add(viewport);

        // Vertical scrollbar indicator
        if (ScrollOrientation != ScrollOrientation.Horizontal)
        {
            root.Children.Add(new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = $"{scrollbarClass} eq-scrollbar-vertical",
                    ["data-orientation"] = "vertical"
                },
                Children = {
                    new DynamicElement
                    {
                        TagName = "div",
                        CustomAttributes = new Dictionary<string, string>
                        {
                            ["class"] = thumbClass
                        }
                    }
                }
            });
        }

        // Horizontal scrollbar indicator
        if (ScrollOrientation != ScrollOrientation.Vertical)
        {
            root.Children.Add(new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = $"{scrollbarClass} eq-scrollbar-horizontal",
                    ["data-orientation"] = "horizontal"
                },
                Children = {
                    new DynamicElement
                    {
                        TagName = "div",
                        CustomAttributes = new Dictionary<string, string>
                        {
                            ["class"] = thumbClass
                        }
                    }
                }
            });
        }

        return root;
    }
}

/// <summary>Scroll direction for ScrollArea.</summary>
public enum ScrollOrientation
{
    Vertical,
    Horizontal,
    Both
}
