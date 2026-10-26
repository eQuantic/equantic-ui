namespace eQuantic.UI.Code;

/// <summary>
/// What a symbol of a document IS, which is the letter and the colour a list draws beside it: every kind
/// of the Language Server Protocol's <c>SymbolKind</c>, so an adapter maps one to one and loses none. The
/// values are this enum's own, appended to, never LSP's numbers (LSP's start at 1).
/// </summary>
public enum CodeSymbolKind : byte
{
    File = 0, Module = 1, Namespace = 2, Package = 3, Class = 4, Method = 5, Property = 6, Field = 7,
    Constructor = 8, Enum = 9, Interface = 10, Function = 11, Variable = 12, Constant = 13, String = 14,
    Number = 15, Boolean = 16, Array = 17, Object = 18, Key = 19, Null = 20, EnumMember = 21, Struct = 22,
    Event = 23, Operator = 24, TypeParameter = 25,
}
