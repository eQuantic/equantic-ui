namespace eQuantic.UI.Code;

/// <summary>
/// Where completions come from. Implemented by the APP: an IDE hands back what its language server
/// said, and a query editor hands back its column names. Asynchronous because the answer usually
/// crosses a process boundary, and an editor that waits for it is an editor that stutters: the list
/// asks once when a word starts and filters what it was given as the word grows
/// (<see cref="CodeCompletion"/>).
/// </summary>
public interface ICodeCompletionProvider
{
    /// <summary>
    /// Characters that open the list unprompted, and ask this provider alone: the dot after a name.
    /// None by default, since a trigger character is a claim about a language.
    /// </summary>
    IReadOnlyList<char> TriggerCharacters => [];

    /// <summary>
    /// What to offer at <paramref name="position"/>. <paramref name="cancellation"/> is cancelled
    /// once the answer can no longer be shown (the word was left, or another request replaced this
    /// one), and an answer that arrives anyway is dropped.
    /// </summary>
    Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation);

    /// <summary>
    /// The item the list has selected, with what was too costly to compute for every item filled in
    /// (its <see cref="CodeCompletionItem.Documentation"/>, its <see cref="CodeCompletionItem.Detail"/>):
    /// LSP's <c>completionItem/resolve</c>. Asked once per item, when it is first selected. The default
    /// has nothing to add.
    /// </summary>
    Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem item, CancellationToken cancellation) =>
        Task.FromResult(item);
}
