using System.Collections.Generic;

namespace eQuantic.UI.Charts;

public class ChartData<T>
{
    public List<string> Labels { get; set; } = new();
    public List<Dataset<T>> Datasets { get; set; } = new();
}
