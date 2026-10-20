namespace eQuantic.UI.Code;

/// <summary>
/// Where an editor's problems come from as its text changes: a compiler, an analyzer, a linter, a
/// server action that builds the buffer. It is asked about the whole document once typing pauses
/// (<see cref="CodeDiagnostics"/>), and its answer replaces what it answered before. The token is
/// cancelled when the next edit makes the question stale; an answer that arrives after that is
/// dropped whether or not the token was honoured.
/// </summary>
public interface ICodeDiagnosticProvider
{
    Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation);
}
