using System.Collections.Generic;

namespace eQuantic.UI.Charts;

public abstract class Dataset<T>
{
    public string? Label { get; set; }
    public List<T> Data { get; set; } = new();
}
