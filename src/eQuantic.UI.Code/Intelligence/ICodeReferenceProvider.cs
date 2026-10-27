namespace eQuantic.UI.Code;

/// <summary>
/// Where what stands at a position is used (#1084): LSP's <c>textDocument/references</c>, its declaration among
/// them, as VS Code asks them. The editor asks when the person finds the references, and an answer that comes
/// after the caret moved or the text changed is dropped.
/// </summary>
public interface ICodeReferenceProvider
{
    Task<IReadOnlyList<CodeLocation>> ReferencesAsync(CodeDocument document, CodePosition position,
        CancellationToken cancellation);
}
