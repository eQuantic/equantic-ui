namespace eQuantic.UI.Code;

/// <summary>
/// One place a stop is written in a copy of a snippet (<see cref="CodeSnippetExpansion"/>): its number, where
/// it starts and ends, relative to the copy's start (a column on the copy's first line counts from the
/// copy's), the number of the placeholder it is nested in, or -1, and its options when it is a choice.
/// </summary>
internal sealed record CodeSnippetPlace(
    int Stop,
    CodePosition Start,
    CodePosition End,
    int Parent,
    IReadOnlyList<string>? Choices)
{
    /// <summary>Where a position relative to a copy's start stands once the copy starts at <paramref name="start"/>.</summary>
    public static CodePosition At(CodePosition start, CodePosition relative) =>
        relative.Line == 0
            ? new CodePosition(start.Line, start.Column + relative.Column)
            : new CodePosition(start.Line + relative.Line, relative.Column);
}
