namespace eQuantic.UI.Code;

/// <summary>
/// That nothing was found where the person asked (#1084): no definition, declaration, type definition or
/// implementation (<see cref="Kind"/>, for <see cref="CodeGoToMode.Definitions"/>), or no reference, for the
/// <see cref="Word"/> at <see cref="At"/>. The editor says it in a card at the caret, as VS Code's message says
/// "No definition found for 'Total'", until the caret moves, the text changes, a key is pressed or the keyboard
/// leaves.
/// </summary>
public sealed record CodeGoToNotFound(CodeGoToMode Mode, CodeDefinitionKind Kind, CodePosition At, string Word);
