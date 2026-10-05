namespace eQuantic.UI.Code;

/// <summary>
/// The words already in the document, each once: what a plain-text editor completes, and what any
/// editor offers before (or without) a language service. The word the caret is in is the one being
/// typed, and is left out; so is a number.
/// </summary>
public sealed class CodeWordCompletionProvider : ICodeCompletionProvider
{
    public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
        CodeCompletionContext context, CancellationToken cancellation)
    {
        var seen = new HashSet<string>();
        var items = new List<CodeCompletionItem>();
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            var i = 0;
            while (i < text.Length)
            {
                if (!CodeDocument.IsWordChar(text[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                while (i < text.Length && CodeDocument.IsWordChar(text[i])) i++;
                if (line == position.Line && start <= position.Column && position.Column <= i) continue;
                if (char.IsDigit(text[start])) continue;
                var word = text.Substring(start, i - start);
                if (seen.Add(word)) items.Add(new CodeCompletionItem(word));
            }
        }
        return Task.FromResult(new CodeCompletionList(items));
    }
}
