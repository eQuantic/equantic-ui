namespace eQuantic.UI.Code;

/// <summary>
/// What explains the call being written: its signatures, the one that applies and the argument the caret
/// is on. LSP's <c>textDocument/signatureHelp</c>, typed, so a language server's client is an adapter, and
/// the authority on the active parameter, as it is in VS Code: a comma counted by the editor is not always
/// an argument's (<c>Dictionary&lt;int, string&gt;</c>, a lambda's parameters).
/// </summary>
public interface ICodeSignatureProvider
{
    /// <summary>The characters that open the panel when typed: an opening parenthesis.</summary>
    IReadOnlyList<char> TriggerCharacters => ['('];

    /// <summary>The characters that ask again at once while the panel shows: a comma. Every other edit or
    /// move asks again too, after the session's pause (<see cref="CodeSignatureHelpSession.Delay"/>).</summary>
    IReadOnlyList<char> RetriggerCharacters => [','];

    /// <summary>The signatures of the call at <paramref name="position"/>, or null, or none, when the
    /// position is in no call: which closes the panel.</summary>
    Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
        CodeSignatureContext context, CancellationToken cancellation);
}
