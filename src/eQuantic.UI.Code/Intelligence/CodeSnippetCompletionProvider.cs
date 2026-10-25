namespace eQuantic.UI.Code;

/// <summary>
/// The snippets an app lists, offered wherever a word starts (#1013): each one an entry named by its prefix,
/// of the snippet kind, its description beside it, which inserts its body as a snippet. What VS Code's snippet
/// files are, written in C#: the built-in provider the completion left for the snippet engine. Never right
/// after a dot, where what follows is a member.
/// </summary>
public sealed class CodeSnippetCompletionProvider : ICodeCompletionProvider
{
    private readonly List<CodeCompletionItem> _items = [];

    public CodeSnippetCompletionProvider(IEnumerable<CodeSnippetDefinition> snippets)
    {
        foreach (var snippet in snippets)
        {
            // Read once, here, rather than on every keystroke the list is asked on.
            _items.Add(new CodeCompletionItem(snippet.Prefix, CodeCompletionKind.Snippet)
            {
                Detail = snippet.Description,
                Snippet = CodeSnippet.Parse(snippet.Body),
            });
        }
    }

    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation)
    {
        var line = document.Line(position.Line);
        var start = position.Column;
        while (start > 0 && CodeDocument.IsWordChar(line[start - 1])) start--;
        if (start > 0 && line[start - 1] == '.') return Task.FromResult(CodeCompletionList.Empty);
        return Task.FromResult(new CodeCompletionList(_items));
    }
}
