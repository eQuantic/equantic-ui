namespace eQuantic.UI.Code;

/// <summary>
/// One part of a parsed snippet: text, a stop or a variable. A stop has its number in <see cref="Index"/>, and
/// every other part -1. A variable is <see cref="Variable"/>, its name in <see cref="Text"/>; text is neither,
/// and inserts <see cref="Text"/>. <see cref="Children"/> are a placeholder's parts, or a variable's default,
/// and null for a bare stop or a variable with none. <see cref="Choices"/> are a choice's options, and null for
/// any other part.
/// </summary>
internal sealed record CodeSnippetPart(
    string Text,
    int Index,
    bool Variable,
    IReadOnlyList<CodeSnippetPart>? Children,
    IReadOnlyList<string>? Choices)
{
    /// <summary>Whether the part is a tab stop, a placeholder or a choice.</summary>
    public bool IsStop => Index >= 0;
}
