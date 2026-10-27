namespace eQuantic.UI.Code;

/// <summary>
/// Folds derived from indentation alone, as VS Code's indentation strategy derives them: a line that is followed by
/// more-indented lines opens a region that ends at the last of them, before the indentation comes back. No parser,
/// no language knowledge, and right often enough to be the default.
/// </summary>
public sealed class IndentationFoldProvider : ICodeFoldProvider
{
    private readonly int _tabSize;

    /// <param name="tabSize">How many columns a tab stands for, the editor's <see cref="CodeLanguageRules.IndentWidth"/>:
    /// an indentation is measured in the columns it is drawn across, as VS Code measures it with its tab size, so a
    /// line indented with a tab and one indented with as many spaces stand at the same depth.</param>
    public IndentationFoldProvider(int tabSize = 4)
    {
        _tabSize = tabSize;
    }

    public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation) =>
        Task.FromResult(FoldsOf(document));

    /// <summary>The columns <paramref name="line"/>'s indentation is drawn across, a tab to its stop.</summary>
    private int DepthOf(CodeDocument document, int line) => CodeLineCells.WidthOf(document.IndentOf(line), _tabSize);

    /// <summary>The regions of <paramref name="document"/>, at once.</summary>
    private IReadOnlyList<CodeFold> FoldsOf(CodeDocument document)
    {
        var folds = new List<CodeFold>();
        for (var line = 0; line < document.LineCount - 1; line++)
        {
            var text = document.Line(line);
            if (text.Trim().Length == 0) continue;

            var indent = DepthOf(document, line);
            var last = line;
            for (var next = line + 1; next < document.LineCount; next++)
            {
                var candidate = document.Line(next);
                if (candidate.Trim().Length == 0) continue;          // blanks belong to the region
                if (DepthOf(document, next) <= indent) break;
                last = next;
            }
            if (last > line) folds.Add(new CodeFold(line, last));
        }
        return folds;
    }
}
