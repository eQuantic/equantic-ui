namespace eQuantic.UI.Code;

/// <summary>The icon and colour a completion carries — the LSP set, trimmed to what a list shows.</summary>
public enum CodeCompletionKind : byte
{
    Text = 0, Method = 1, Function = 2, Constructor = 3, Field = 4, Variable = 5, Class = 6,
    Interface = 7, Module = 8, Property = 9, Enum = 10, Keyword = 11, Snippet = 12, File = 13,
}
