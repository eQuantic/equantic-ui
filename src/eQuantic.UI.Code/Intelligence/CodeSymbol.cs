namespace eQuantic.UI.Code;

/// <summary>
/// One thing a document declares, as a language service answers it: LSP's <c>DocumentSymbol</c>, typed.
/// <see cref="Range"/> is all of it, the body and the comments a language counts with it, and
/// <see cref="SelectionRange"/> the part that names it, which is where going to it puts the caret. The
/// symbols it holds are its <see cref="Children"/>, so a class answers its members and a namespace its
/// types.
/// </summary>
public sealed record CodeSymbol(string Name, CodeSymbolKind Kind, CodeRange Range, CodeRange SelectionRange)
{
    /// <summary>What a list shows beside the name: a signature, a type.</summary>
    public string? Detail { get; init; }

    /// <summary>Whether it is deprecated, which a list shows struck through (LSP's
    /// <c>SymbolTag.Deprecated</c>).</summary>
    public bool Deprecated { get; init; }

    /// <summary>The symbols it holds, in the order the document holds them.</summary>
    public IReadOnlyList<CodeSymbol> Children { get; init; } = [];
}
