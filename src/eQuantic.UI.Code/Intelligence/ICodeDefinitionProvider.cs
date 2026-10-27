namespace eQuantic.UI.Code;

/// <summary>
/// Where what stands at a position is defined (#1084): LSP's <c>textDocument/definition</c>,
/// <c>declaration</c>, <c>typeDefinition</c> and <c>implementation</c>, four requests of one shape asked here
/// as one, told which by its <c>kind</c>. A provider that does not answer a kind answers nothing. The
/// editor asks when the person goes to a definition and while ⌘ is held over a name, and an answer that comes
/// after the caret moved or the text changed is dropped, so a provider may take its time and should honour the
/// token.
/// </summary>
public interface ICodeDefinitionProvider
{
    Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
        CodeDefinitionKind kind, CancellationToken cancellation);
}
