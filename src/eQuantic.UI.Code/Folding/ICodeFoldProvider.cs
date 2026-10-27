namespace eQuantic.UI.Code;

/// <summary>
/// Where the regions that fold come from (#1119): LSP's <c>textDocument/foldingRange</c>. The editor asks after the
/// text has rested, and an answer that comes after it changed again is dropped, so a provider may take its time
/// and should honour the token. With none, the editor folds by indentation (<see cref="IndentationFoldProvider"/>),
/// which works for every language, those with no braces included; an app with a parser hands better ones.
/// </summary>
public interface ICodeFoldProvider
{
    Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation);
}
