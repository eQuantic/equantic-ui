namespace eQuantic.UI.Code;

/// <summary>
/// What a provider is told besides where: why it was asked, the character that asked when one did,
/// and the language of the editor asking, which LSP carries on the document as its language id.
/// </summary>
public sealed record CodeCompletionContext(CodeCompletionTrigger Trigger, ICodeLanguage Language)
{
    /// <summary>The character typed, for <see cref="CodeCompletionTrigger.Character"/> and
    /// <see cref="CodeCompletionTrigger.Typing"/>, and null otherwise.</summary>
    public char? Character { get; init; }
}
