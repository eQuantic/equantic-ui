using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Data Table theme implementation
/// </summary>
public class MaterialTableTheme : ITableTheme
{
    public string Wrapper => "md-data-table-wrapper";
    public string Table => "md-data-table";
    public string Header => "md-data-table__header";
    public string Row => "md-data-table__row";
    public string HeadCell => "md-data-table__head-cell";
    public string Cell => "md-data-table__cell";
}
