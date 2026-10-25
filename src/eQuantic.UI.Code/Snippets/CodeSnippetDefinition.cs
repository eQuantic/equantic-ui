namespace eQuantic.UI.Code;

/// <summary>
/// A snippet an app offers by name (#1013), as a VS Code snippet file lists one: the <paramref name="Prefix"/>
/// a person types to find it, the <paramref name="Body"/> it inserts, in the snippet syntax
/// (<see cref="CodeSnippet"/>), and the <paramref name="Description"/> shown beside it.
/// <see cref="CodeSnippetCompletionProvider"/> offers a list of them.
/// </summary>
public sealed record CodeSnippetDefinition(string Prefix, string Body, string? Description = null);
