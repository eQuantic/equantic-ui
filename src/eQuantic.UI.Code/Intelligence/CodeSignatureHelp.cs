namespace eQuantic.UI.Code;

/// <summary>
/// What a signature provider answers: the call's signatures, the one that applies and the argument the
/// caret is on. LSP's <c>SignatureHelp</c>.
/// </summary>
public sealed record CodeSignatureHelp(IReadOnlyList<CodeSignature> Signatures)
{
    /// <summary>The signature that applies, an index into <see cref="Signatures"/>.</summary>
    public int ActiveSignature { get; init; }

    /// <summary>The argument the caret is on, an index into the active signature's parameters, unless that
    /// signature says its own (<see cref="CodeSignature.ActiveParameter"/>).</summary>
    public int ActiveParameter { get; init; }
}
