namespace eQuantic.UI.Code;

/// <summary>
/// What one provider answered for an open list, and how long the caret's line was when it was asked:
/// an item's <see cref="CodeCompletionItem.Replacing"/> is a range in the document as it was then,
/// and its end keeps its distance from the end of the line.
/// </summary>
internal sealed class CodeCompletionAnswer
{
    public CodeCompletionAnswer(ICodeCompletionProvider provider, int askedLineLength, CodeCompletionList list)
    {
        Provider = provider;
        AskedLineLength = askedLineLength;
        IsIncomplete = list.IsIncomplete;
        foreach (var item in list.Items) Offers.Add(new CodeCompletionOffer(this, item));
    }

    public ICodeCompletionProvider Provider { get; }

    /// <summary>How long the caret's line was when the provider was asked: an item's range ends as far
    /// from the end of the line as it did then, wherever the caret has moved since.</summary>
    public int AskedLineLength { get; }

    /// <summary>Whether the provider asked to be asked again as the word changes.</summary>
    public bool IsIncomplete { get; }

    public List<CodeCompletionOffer> Offers { get; } = [];
}
