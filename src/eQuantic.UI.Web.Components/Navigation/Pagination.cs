using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Navigation;

/// <summary>
/// Pagination component for navigating between pages of content.
///
/// Usage:
/// <code>
/// new Pagination {
///     TotalPages = 20,
///     CurrentPage = 5,
///     SiblingCount = 1
/// }
/// </code>
/// </summary>
public class Pagination : StatelessComponent
{
    /// <summary>Total number of pages.</summary>
    public int TotalPages { get; set; } = 1;

    /// <summary>Currently active page (1-based). Default: 1.</summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of sibling pages visible on each side of the current page. Default: 1.
    /// Example: SiblingCount=1 → 1 ... 4 [5] 6 ... 20
    /// </summary>
    public int SiblingCount { get; set; } = 1;

    /// <summary>Whether to show the Previous/Next buttons. Default: true.</summary>
    public bool ShowPrevNext { get; set; } = true;

    /// <summary>Text for the previous button. Default: "Previous".</summary>
    public string PreviousLabel { get; set; } = "Previous";

    /// <summary>Text for the next button. Default: "Next".</summary>
    public string NextLabel { get; set; } = "Next";

    /// <summary>
    /// Accessible label for the navigation element. Default: "Pagination".
    /// </summary>
    public string AriaLabel { get; set; } = "Pagination";

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var paginationTheme = theme?.Pagination;

        var containerClass = paginationTheme?.Container ?? "eq-pagination";
        var listClass = paginationTheme?.List ?? "eq-pagination-list";
        var itemClass = paginationTheme?.Item ?? "eq-pagination-item";
        var activeClass = paginationTheme?.Active ?? "eq-pagination-active";
        var disabledClass = paginationTheme?.Disabled ?? "eq-pagination-disabled";
        var ellipsisClass = paginationTheme?.Ellipsis ?? "eq-pagination-ellipsis";

        var nav = new Box
        {
            As = "nav",
            ClassName = $"{containerClass} {ClassName}".Trim(),
            Role = "navigation",
            AriaLabel = AriaLabel
        };

        var list = new Box
        {
            As = "ul",
            ClassName = listClass
        };

        // Previous button
        if (ShowPrevNext)
        {
            var prevDisabled = CurrentPage <= 1;
            list.Children.Add(CreateNavItem(
                PreviousLabel,
                $"{itemClass} {(prevDisabled ? disabledClass : "")}".Trim(),
                prevDisabled,
                "prev",
                "Go to previous page"
            ));
        }

        // Page numbers with ellipsis
        var pages = CalculatePageRange();
        foreach (var page in pages)
        {
            if (page == -1)
            {
                // Ellipsis
                list.Children.Add(new Box
                {
                    As = "li",
                    Children = {
                        new Box
                        {
                            As = "span",
                            ClassName = ellipsisClass,
                            AriaHidden = true,
                            Children = { new Text("…") }
                        }
                    }
                });
            }
            else
            {
                var isActive = page == CurrentPage;
                var pageClass = isActive ? $"{itemClass} {activeClass}" : itemClass;

                list.Children.Add(new Box
                {
                    As = "li",
                    Children = {
                        new Box
                        {
                            As = "button",
                            ClassName = pageClass,
                            Type = "button",
                            AriaCurrent = isActive ? "page" : null,
                            Children = { new Text(page.ToString()) }
                        }
                    }
                });
            }
        }

        // Next button
        if (ShowPrevNext)
        {
            var nextDisabled = CurrentPage >= TotalPages;
            list.Children.Add(CreateNavItem(
                NextLabel,
                $"{itemClass} {(nextDisabled ? disabledClass : "")}".Trim(),
                nextDisabled,
                "next",
                "Go to next page"
            ));
        }

        nav.Children.Add(list);
        return nav;
    }

    private Box CreateNavItem(string text, string cssClass, bool disabled, string rel, string ariaLabel)
    {
        var btn = new Box
        {
            As = "button",
            ClassName = cssClass,
            Type = "button",
            AriaLabel = ariaLabel,
            Children = { new Text(text) }
        };

        if (disabled)
        {
            btn.Disabled = true;
            btn.AriaDisabled = true;
        }

        return new Box
        {
            As = "li",
            Children = { btn }
        };
    }

    /// <summary>
    /// Calculates which page numbers to show, using -1 to represent ellipsis.
    /// Algorithm: Always show first and last page, current page ± SiblingCount,
    /// and ellipsis where gaps exist.
    /// </summary>
    private List<int> CalculatePageRange()
    {
        var result = new List<int>();

        if (TotalPages <= 0) return result;

        // If total pages fit within 2*sibling + 5 (first + last + current + 2 ellipsis), show all
        var totalSlots = SiblingCount * 2 + 5;
        if (TotalPages <= totalSlots)
        {
            for (var i = 1; i <= TotalPages; i++) result.Add(i);
            return result;
        }

        var leftSiblingIndex = Math.Max(CurrentPage - SiblingCount, 1);
        var rightSiblingIndex = Math.Min(CurrentPage + SiblingCount, TotalPages);

        var showLeftEllipsis = leftSiblingIndex > 2;
        var showRightEllipsis = rightSiblingIndex < TotalPages - 1;

        // Always include page 1
        result.Add(1);

        if (showLeftEllipsis)
        {
            result.Add(-1); // ellipsis
        }
        else
        {
            // Pages 2..leftSiblingIndex-1
            for (var i = 2; i < leftSiblingIndex; i++) result.Add(i);
        }

        // Sibling range including current page
        for (var i = leftSiblingIndex; i <= rightSiblingIndex; i++)
        {
            if (i != 1 && i != TotalPages) result.Add(i);
        }

        if (showRightEllipsis)
        {
            result.Add(-1); // ellipsis
        }
        else
        {
            // Pages rightSiblingIndex+1..TotalPages-1
            for (var i = rightSiblingIndex + 1; i < TotalPages; i++) result.Add(i);
        }

        // Always include last page
        if (TotalPages > 1) result.Add(TotalPages);

        return result;
    }
}
