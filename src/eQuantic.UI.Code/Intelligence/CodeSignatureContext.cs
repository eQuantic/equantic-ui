namespace eQuantic.UI.Code;

/// <summary>Why a signature provider is asked: LSP's <c>SignatureHelpContext</c>.</summary>
public sealed record CodeSignatureContext(CodeSignatureTrigger Trigger)
{
    /// <summary>The character typed, for <see cref="CodeSignatureTrigger.Character"/>.</summary>
    public char? Character { get; init; }

    /// <summary>Whether the panel was showing when it was asked: the provider can go on from what it
    /// showed.</summary>
    public bool IsRetrigger { get; init; }

    /// <summary>What the panel shows while it shows, with the signature the person chose with the arrows
    /// as its active one.</summary>
    public CodeSignatureHelp? Active { get; init; }
}
