namespace eQuantic.UI.Code;

/// <summary>What a row of a view shows (see <see cref="CodeRows"/>).</summary>
public enum CodeRowKind
{
    /// <summary>A line of the document.</summary>
    Line,

    /// <summary>A row that belongs to no line: padding, or a line of another document.</summary>
    Filler,

    /// <summary>One row standing for a run of hidden lines.</summary>
    Placeholder,
}
