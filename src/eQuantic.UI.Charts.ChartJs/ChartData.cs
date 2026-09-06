namespace eQuantic.UI.Charts;

/// <summary>The labels and datasets a <c>ChartJs&lt;T&gt;</c> plots. Kept in the namespace pages
/// already import — <c>eQuantic.UI.Charts</c> — while the wrapper lives; the write-once library that
/// owns that namespace now uses none of these names.</summary>
public class ChartData<T>
{
    public List<string> Labels { get; set; } = new();
    public List<Dataset<T>> Datasets { get; set; } = new();
}

/// <summary>One dataset of a <see cref="ChartData{T}"/>.</summary>
public abstract class Dataset<T>
{
    public string? Label { get; set; }
    public List<T> Data { get; set; } = new();
}
