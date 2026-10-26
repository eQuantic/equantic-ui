namespace eQuantic.UI.Code;

/// <summary>
/// One symbol the go-to field lists (#1083): its name and kind, the <see cref="Range"/> washed while it is
/// selected, and the <see cref="Target"/> the caret goes to, where its name starts. The characters of the
/// name and of its container that the typed text matched are <see cref="Matches"/> and
/// <see cref="ContainerMatches"/>, for a list to mark.
/// </summary>
public sealed record CodeGoToEntry(string Name, CodeSymbolKind Kind, CodeRange Range, CodePosition Target)
{
    /// <summary>The name of the symbol that holds it, or null at the top of the document.</summary>
    public string? Container { get; init; }

    /// <summary>Whether it is deprecated, which a list shows struck through.</summary>
    public bool Deprecated { get; init; }

    /// <summary>Where in <see cref="Name"/> the typed text matched.</summary>
    public IReadOnlyList<int> Matches { get; init; } = [];

    /// <summary>Where in <see cref="Container"/> the typed text matched.</summary>
    public IReadOnlyList<int> ContainerMatches { get; init; } = [];
}
