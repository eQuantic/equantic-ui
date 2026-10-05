namespace eQuantic.UI.Code;

/// <summary>
/// One item of an answer, kept with the answer it came in: accepting it reads where the provider
/// was asked, and resolving it asks that provider again and puts what it says in its place.
/// </summary>
internal sealed class CodeCompletionOffer
{
    public CodeCompletionOffer(CodeCompletionAnswer answer, CodeCompletionItem item)
    {
        Answer = answer;
        Item = item;
    }

    public CodeCompletionAnswer Answer { get; }

    /// <summary>The item, replaced by what its provider resolves it to.</summary>
    public CodeCompletionItem Item { get; set; }

    /// <summary>Whether its provider has been asked to resolve it, so it is asked once.</summary>
    public bool Resolving { get; set; }
}
