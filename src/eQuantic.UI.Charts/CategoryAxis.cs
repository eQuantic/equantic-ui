namespace eQuantic.UI.Charts;

/// <summary>The category axis: the names the bars are grouped by, in order.</summary>
/// <param name="Categories">One label per category.</param>
/// <param name="Title">The axis title, when the categories need one.</param>
public sealed record CategoryAxis(IReadOnlyList<string> Categories, string? Title = null);
