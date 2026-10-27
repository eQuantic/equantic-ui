namespace eQuantic.UI.Code;

/// <summary>Which of the four questions LSP asks about a name a definition provider is asked (#1084): where it
/// is defined, declared, typed or implemented.</summary>
public enum CodeDefinitionKind : byte
{
    /// <summary>Where it is defined: LSP's <c>textDocument/definition</c>, which F12 asks.</summary>
    Definition = 0,
    /// <summary>Where it is declared, for a language that tells a declaration from a definition:
    /// <c>textDocument/declaration</c>.</summary>
    Declaration = 1,
    /// <summary>Where the type of what it is is defined: <c>textDocument/typeDefinition</c>.</summary>
    TypeDefinition = 2,
    /// <summary>Where it is implemented: <c>textDocument/implementation</c>, which ⌘F12 asks.</summary>
    Implementation = 3,
}
