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
        var overflowStyleX = ScrollOrientation switch
        {
            ScrollOrientation.Horizontal => "auto",
            ScrollOrientation.Vertical => "hidden",
            _ => "auto"
        };
        
        var overflowStyleY = ScrollOrientation switch
        {
            ScrollOrientation.Horizontal => "hidden",
            ScrollOrientation.Vertical => "auto",
            _ => "auto"
        };

        var root = new Box
        {
            As = "div",
            ClassName = $"{rootClass} {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["orientation"] = ScrollOrientation.ToString().ToLowerInvariant() }
        };

        // Viewport
        var viewport = new Box
        {
            As = "div",
            ClassName = viewportClass,
            Style = new HtmlStyle { Width = "100%", Height = "100%", Position = eQuantic.UI.Core.Position.Relative, OverflowX = overflowStyleX, OverflowY = overflowStyleY },
            TabIndex = 0
        };

        foreach (var child in Children) viewport.Children.Add(child);

        root.Children.Add(viewport);

        // Vertical scrollbar indicator
        if (ScrollOrientation != ScrollOrientation.Horizontal)
        {
            root.Children.Add(new Box
            {
                As = "div",
                ClassName = $"{scrollbarClass} eq-scrollbar-vertical",
                DataAttributes = new Dictionary<string, string> { ["orientation"] = "vertical" },
                Children = {
                    new Box
                    {
                        As = "div",
                        ClassName = thumbClass
                    }
                }
            });
        }

        // Horizontal scrollbar indicator
        if (ScrollOrientation != ScrollOrientation.Vertical)
        {
            root.Children.Add(new Box
            {
                As = "div",
                ClassName = $"{scrollbarClass} eq-scrollbar-horizontal",
                DataAttributes = new Dictionary<string, string> { ["orientation"] = "horizontal" },
                Children = {
                    new Box
                    {
                        As = "div",
                        ClassName = thumbClass
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
