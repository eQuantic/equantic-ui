namespace eQuantic.UI.Code;

/// <summary>
/// Where the symbols of a document come from: LSP's <c>textDocument/documentSymbol</c>, answered as a
/// tree. The editor asks when the person goes to a symbol, and an answer that comes after the person
/// moved on is dropped, so a provider may take its time and should honour the token.
/// </summary>
public interface ICodeSymbolProvider
{
    Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation);
}
