namespace eQuantic.UI.Code;

/// <summary>
/// A provider's answer: what it offers, and whether that is everything (LSP's <c>CompletionList</c>).
/// A COMPLETE answer is only filtered as the word grows. An INCOMPLETE one is asked again whenever
/// the word changes, which is what a provider that caps its list, or narrows on a server, answers.
/// </summary>
public sealed record CodeCompletionList(IReadOnlyList<CodeCompletionItem> Items, bool IsIncomplete = false)
{
    /// <summary>Nothing to offer, and nothing more to ask for.</summary>
    public static readonly CodeCompletionList Empty = new([]);
}
