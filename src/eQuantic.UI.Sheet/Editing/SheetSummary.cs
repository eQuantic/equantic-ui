namespace eQuantic.UI.Sheet;

/// <summary>
/// What a status bar shows of the selection (#984): the cells in use the ranges cover, how many of them hold
/// a number, and those numbers' sum and average, each cell counted once however many ranges cover it.
/// <see cref="Sum"/> and <see cref="Average"/> are null when the sum passes what a decimal holds, and the
/// average when no cell holds a number.
/// </summary>
public readonly record struct SheetSummary(int Count, int Numbers, decimal? Sum, decimal? Average);
