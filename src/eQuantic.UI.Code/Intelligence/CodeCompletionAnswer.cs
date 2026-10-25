namespace eQuantic.UI.Code;

/// <summary>
/// What one provider answered for an open list, the word it was asked for, and how many of the list's edits
/// had been made when it was asked: an item's <see cref="CodeCompletionItem.Replacing"/> is a range in the
/// document as it was then, carried through every edit made since.
/// </summary>
internal sealed class CodeCompletionAnswer
{
    // The last range carried (Carried): where it stood when asked, where it stands, and how many of the
    // list's edits it went through.
    private CodeRange _from;
    private CodeRange _to;
    private int _through = -1;

    public CodeCompletionAnswer(ICodeCompletionProvider provider, string askedWord, int asked,
        CodeCompletionList list)
    {
        Provider = provider;
        AskedWord = askedWord;
        Asked = asked;
        IsIncomplete = list.IsIncomplete;
        foreach (var item in list.Items) Offers.Add(new CodeCompletionOffer(this, item));
    }

    public ICodeCompletionProvider Provider { get; }

    /// <summary>The word the provider was asked for: an incomplete answer is asked for again once the
    /// word is another.</summary>
    public string AskedWord { get; }

    /// <summary>How many of the list's edits had been made when the provider was asked: an item's range
    /// stands where the edits made after them carried it, at the caret and at every other caret.</summary>
    public int Asked { get; }

    /// <summary>
    /// Where <paramref name="range"/>, an item's range, stands once carried through <paramref name="edits"/>,
    /// the list's edits, from the first one made after the provider was asked. The last range carried is
    /// kept with the edits it went through: an answer's items mostly share one range, carried once a
    /// keystroke rather than once an item, through the edits that keystroke made.
    /// </summary>
    public CodeRange Carried(CodeRange range, IReadOnlyList<CodeEdit> edits)
    {
        if (_through < 0 || range != _from)
        {
            _from = range;
            _to = range;
            _through = Asked;
        }
        for (; _through < edits.Count; _through++)
            _to = new CodeRange(edits[_through].CarryAnchor(_to.Start), edits[_through].CarryAnchor(_to.End));
        return _to;
    }

    /// <summary>Whether the provider asked to be asked again as the word changes.</summary>
    public bool IsIncomplete { get; }

    public List<CodeCompletionOffer> Offers { get; } = [];
}
