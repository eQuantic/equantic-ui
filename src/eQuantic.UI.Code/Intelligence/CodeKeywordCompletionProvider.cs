namespace eQuantic.UI.Code;

/// <summary>
/// The words of the editor's language (<see cref="ICodeLanguage.Keywords"/>), offered wherever a
/// word starts: what an editor with no language service completes besides the document's own words.
/// Never right after a dot, where what follows is a member and never a keyword.
/// </summary>
public sealed class CodeKeywordCompletionProvider : ICodeCompletionProvider
{
    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation)
    {
        var line = document.Line(position.Line);
        var start = position.Column;
        while (start > 0 && CodeDocument.IsWordChar(line[start - 1])) start--;
        if (start > 0 && line[start - 1] == '.') return Task.FromResult(CodeCompletionList.Empty);

        var items = new List<CodeCompletionItem>();
        foreach (var keyword in context.Language.Keywords)
            items.Add(new CodeCompletionItem(keyword, CodeCompletionKind.Keyword));
        return Task.FromResult(new CodeCompletionList(items));
    }
}
