namespace eQuantic.UI.Code;

/// <summary>
/// Where completions come from. Implemented by the APP — an IDE hands back what its language
/// server said; a form editor hands back column names. Asynchronous because the answer usually
/// crosses a process boundary, and an editor that blocks on it is an editor that stutters.
/// </summary>
public interface ICodeCompletionProvider
{
    /// <summary>Characters that OPEN the list unprompted — a dot, a slash inside a path.</summary>
    IReadOnlyList<char> TriggerCharacters => ['.'];

    Task<IReadOnlyList<CodeCompletionItem>> CompleteAsync(
        CodeDocument document, CodePosition position, CancellationToken cancellation);
}
