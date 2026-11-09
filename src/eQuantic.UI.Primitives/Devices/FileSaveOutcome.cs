namespace eQuantic.UI.Primitives;

/// <summary>
/// What became of a file a page offered through <see cref="IFileSaver"/>. Each target answers what it
/// honestly knows, and no more.
/// </summary>
public enum FileSaveOutcome
{
    /// <summary>
    /// The target took the file and saves it the way it saves downloads. A browser answers this: it
    /// tells a page nothing about where the file went, or whether the person kept it.
    /// </summary>
    Offered,

    /// <summary>The file was written where the person chose, in a save panel.</summary>
    Saved,

    /// <summary>The person closed the panel without choosing a place. Nothing was written, and
    /// nothing failed.</summary>
    Cancelled,
}
