namespace eQuantic.UI.Code;

/// <summary>
/// A snippet's choice as the completion's list asks it (#1013): its options, in their order, each a value.
/// The sort text keeps the order the snippet wrote them in where nothing typed ranks one above another.
/// </summary>
internal sealed class CodeChoiceProvider : ICodeCompletionProvider
{
    private readonly CodeCompletionList _list;

    public CodeChoiceProvider(IReadOnlyList<string> options)
    {
        var items = new List<CodeCompletionItem>();
        for (var i = 0; i < options.Count; i++)
            items.Add(new CodeCompletionItem(options[i], CodeCompletionKind.Value) { SortText = i.ToString().PadLeft(6, '0') });
        _list = new CodeCompletionList(items);
    }

    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation) => Task.FromResult(_list);
}
