namespace eQuantic.UI.Core.Theme;

/// <summary>EQ (first-party) styling for the Pagination component.</summary>
public class PaginationThemeEQ : IPaginationTheme
{
    public string Container => "eq-pagination";
    public string List => "eq-pagination-list";
    public string Item => "eq-pagination-item";
    public string Active => "eq-pagination-item-active";
    public string Disabled => "eq-pagination-item-disabled";
    public string Ellipsis => "eq-pagination-ellipsis";
}
