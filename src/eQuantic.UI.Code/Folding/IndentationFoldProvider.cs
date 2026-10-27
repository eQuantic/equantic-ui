namespace eQuantic.UI.Code;

/// <summary>
/// Folds derived from indentation alone, as VS Code's indentation strategy derives them: a line that is followed by
/// more-indented lines opens a region that ends at the last of them, before the indentation comes back. No parser,
/// no language knowledge, and right often enough to be the default.
/// </summary>
public sealed class IndentationFoldProvider : ICodeFoldProvider
{
    public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation) =>
        Task.FromResult(FoldsOf(document));

    /// <summary>The regions of <paramref name="document"/>, at once.</summary>
    private static IReadOnlyList<CodeFold> FoldsOf(CodeDocument document)
    {
        var folds = new List<CodeFold>();
        for (var line = 0; line < document.LineCount - 1; line++)
        {
            var text = document.Line(line);
            if (text.Trim().Length == 0) continue;

            var indent = document.IndentOf(line).Length;
            var last = line;
            for (var next = line + 1; next < document.LineCount; next++)
            {
                var candidate = document.Line(next);
                if (candidate.Trim().Length == 0) continue;          // blanks belong to the region
                if (document.IndentOf(next).Length <= indent) break;
                last = next;
            }
            if (last > line) folds.Add(new CodeFold(line, last));
        }
        return folds;
    }
}
