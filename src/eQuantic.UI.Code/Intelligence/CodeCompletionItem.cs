namespace eQuantic.UI.Code;

/// <summary>What a completion offers, and what accepting it does.</summary>
public sealed record CodeCompletionItem(string Label, CodeCompletionKind Kind = CodeCompletionKind.Text)
{
    /// <summary>What is actually inserted — defaults to the label.</summary>
    public string? InsertText { get; init; }

    /// <summary>The one-line explanation beside the label (a signature, a type).</summary>
    public string? Detail { get; init; }

    /// <summary>The longer text a details pane shows.</summary>
    public string? Documentation { get; init; }

    /// <summary>Sorts above the rest when set — a language server's own ranking.</summary>
    public string? SortText { get; init; }

    /// <summary>The range this replaces; null = the word being typed.</summary>
    public CodeRange? Replacing { get; init; }
}
