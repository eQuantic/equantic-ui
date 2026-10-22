namespace eQuantic.UI.Code;

/// <summary>
/// One parameter of a signature: its label as it stands in the signature's label, which is where the panel
/// marks it while it is the argument the caret is on (<see cref="CodeSignature.ParameterStart"/>). LSP's
/// <c>ParameterInformation</c>, whose label may also be offsets: an adapter turns them into the text they
/// cover.
/// </summary>
public sealed record CodeSignatureParameter(string Label)
{
    /// <summary>What the parameter is for, shown under the signature while it is the argument the caret
    /// is on.</summary>
    public string? Documentation { get; init; }
}
