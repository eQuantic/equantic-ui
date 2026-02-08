using System.Collections.Generic;

namespace eQuantic.UI.Charts.ChartJs.Models;

public class ChartJsDataset<T> : Dataset<T>
{
    public string? BackgroundColor { get; set; }
    public string? BorderColor { get; set; }
    public double? BorderWidth { get; set; }
    public bool? Fill { get; set; }
    public double? Tension { get; set; }
    public string? Type { get; set; } // 'line', 'bar', etc.
}
