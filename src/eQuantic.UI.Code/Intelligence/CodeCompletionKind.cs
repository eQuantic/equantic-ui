namespace eQuantic.UI.Code;

/// <summary>
/// What a completion IS, which is the icon a list draws beside it: every kind of the Language Server
/// Protocol's <c>CompletionItemKind</c>, so an adapter maps one to one and loses none. The values
/// are this enum's own, appended to, never LSP's numbers.
/// </summary>
public enum CodeCompletionKind : byte
{
    Text = 0, Method = 1, Function = 2, Constructor = 3, Field = 4, Variable = 5, Class = 6,
    Interface = 7, Module = 8, Property = 9, Enum = 10, Keyword = 11, Snippet = 12, File = 13,
    Struct = 14, EnumMember = 15, Constant = 16, Event = 17, Operator = 18, TypeParameter = 19,
    Value = 20, Unit = 21, Color = 22, Reference = 23, Folder = 24,
}
