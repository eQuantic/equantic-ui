namespace eQuantic.UI.Code;

/// <summary>A collapsible region: the lines it hides when folded.</summary>
public sealed record CodeFold(int StartLine, int EndLine)
{
    /// <summary>What the collapsed line shows instead — "{ … }", "#region Layout".</summary>
    public string? Placeholder { get; init; }
}
