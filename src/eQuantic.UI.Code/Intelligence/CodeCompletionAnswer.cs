namespace eQuantic.UI.Code;

/// <summary>
/// What one provider answered for an open list, and where the caret stood when it was asked: an
/// item's <see cref="CodeCompletionItem.Replacing"/> is a range in the document as it was then, and
/// whatever was typed since moves its end.
/// </summary>
internal sealed class CodeCompletionAnswer
{
    public CodeCompletionAnswer(ICodeCompletionProvider provider, CodePosition askedAt, CodeCompletionList list)
    {
        Provider = provider;
        AskedAt = askedAt;
        IsIncomplete = list.IsIncomplete;
        foreach (var item in list.Items) Offers.Add(new CodeCompletionOffer(this, item));
    }

    public ICodeCompletionProvider Provider { get; }

    public CodePosition AskedAt { get; }

    /// <summary>Whether the provider asked to be asked again as the word changes.</summary>
    public bool IsIncomplete { get; }

    public List<CodeCompletionOffer> Offers { get; } = [];
}
