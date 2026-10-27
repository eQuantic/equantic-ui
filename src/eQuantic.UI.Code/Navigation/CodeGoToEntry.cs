namespace eQuantic.UI.Code;

/// <summary>
/// One row the go-to field lists: a symbol (#1083) or a place (#1084). A symbol has its name and kind; a place
/// has no kind, and its name is the text of its line, or the name of the document it is in when that is not the
/// editor's. The <see cref="Range"/> is washed while it is selected, and the <see cref="Target"/> is where the
/// caret goes: where a symbol's name starts, where a place starts. The characters of the name and of its
/// container the list marks are <see cref="Matches"/> and <see cref="ContainerMatches"/>: what the typed text
/// matched, and a place's name where nothing is typed.
/// </summary>
public sealed record CodeGoToEntry(string Name, CodeSymbolKind? Kind, CodeRange Range, CodePosition Target)
{
    /// <summary>What a row shows beside its name: the name of the symbol that holds a symbol, or null at the top
    /// of the document, and where a place stands, its line and column.</summary>
    public string? Container { get; init; }

    /// <summary>Whether it is deprecated, which a list shows struck through.</summary>
    public bool Deprecated { get; init; }

    /// <summary>The document a place is in when it is not the editor's, as its language service names it.</summary>
    public string? Document { get; init; }

    /// <summary>Where in <see cref="Name"/> the list marks: what the typed text matched, or a place's name.</summary>
    public IReadOnlyList<int> Matches { get; init; } = [];

    /// <summary>Where in <see cref="Container"/> the typed text matched.</summary>
    public IReadOnlyList<int> ContainerMatches { get; init; } = [];
}
