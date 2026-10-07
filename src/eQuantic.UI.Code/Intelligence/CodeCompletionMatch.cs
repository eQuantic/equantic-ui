namespace eQuantic.UI.Code;

/// <summary>
/// One line of an open completion list: the item, how well it matched the word being typed, and
/// which characters of its LABEL that word matched, for the list to mark.
/// </summary>
public sealed record CodeCompletionMatch(CodeCompletionItem Item, int Score, IReadOnlyList<int> Highlights);
