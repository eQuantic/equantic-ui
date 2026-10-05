namespace eQuantic.UI.Code;

/// <summary>
/// What a completion offers, and what accepting it does: the Language Server Protocol's
/// <c>CompletionItem</c>, typed where it is stringly, so an LSP client is an adapter and nothing more.
/// </summary>
public sealed record CodeCompletionItem(string Label, CodeCompletionKind Kind = CodeCompletionKind.Text)
{
    /// <summary>What is actually inserted. Null inserts the label.</summary>
    public string? InsertText { get; init; }

    /// <summary>
    /// What the word being typed is matched against. Null matches the label. A provider sets it when
    /// the label carries more than a name (<c>List&lt;T&gt;</c>, a signature) and the name is what a
    /// person types.
    /// </summary>
    public string? FilterText { get; init; }

    /// <summary>
    /// Orders the items that match equally well, before the label does: a language server's own
    /// ranking. Null sorts by the label.
    /// </summary>
    public string? SortText { get; init; }

    /// <summary>The one-line explanation beside the label (a signature, a type).</summary>
    public string? Detail { get; init; }

    /// <summary>The longer text a details pane shows. A provider may leave it for
    /// <see cref="ICodeCompletionProvider.ResolveAsync"/> to fill in.</summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The range accepting replaces, on the line of the position the provider was asked about and as
    /// the document stood when it answered: LSP's edit range. Its end moves with whatever is typed
    /// after the answer. Null replaces the word typed before the caret.
    /// </summary>
    public CodeRange? Replacing { get; init; }

    /// <summary>Selected when the list opens, among the items that match best: what a language
    /// server knows the person most likely wants next.</summary>
    public bool Preselect { get; init; }

    /// <summary>
    /// Characters that ACCEPT this item when typed while it is selected, and are then typed after
    /// it: <c>Console.</c> accepts <c>Console</c> and goes on to its members. Null accepts on none.
    /// </summary>
    public IReadOnlyList<char>? CommitCharacters { get; init; }
}
