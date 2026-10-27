using eQuantic.UI.Code;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// Where a name of the code screen's file is defined and used, for F12, ⌘F12, ⇧F12 and ⌘ over a name, read from
/// the text and from what <see cref="LedgerSymbols"/> declares: a member of the class the caret is in before one
/// of any other, a class by its name, an override's implementations in every class, and every whole word
/// for the references. A name the file uses and does not declare (<c>DateTime</c>, <c>ToString</c>) is defined in
/// another document, which the screen is asked to open. A language server answers the same questions for a real
/// app, as LSP's <c>textDocument/definition</c> and <c>textDocument/references</c>.
/// </summary>
public sealed class LedgerNames : ICodeDefinitionProvider, ICodeReferenceProvider
{
    private readonly LedgerSymbols _symbols;

    public LedgerNames(LedgerSymbols symbols) => _symbols = symbols;

    /// <summary>The framework's names the file uses, each defined in the framework's own source.</summary>
    private static readonly string[] Framework =
        ["DateTime", "List", "ArgumentOutOfRangeException", "Sum", "ToString", "decimal", "string", "Object"];

    public async Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
        CodeDefinitionKind kind, CancellationToken cancellation)
    {
        if (NameAt(document, position) is not { } name) return [];
        var outline = await _symbols.SymbolsAsync(document, cancellation);
        var classes = new List<CodeSymbol>();
        foreach (var symbol in outline)
            if (symbol.Kind == CodeSymbolKind.Class) classes.Add(symbol);

        var found = new List<CodeLocation>();
        if (kind == CodeDefinitionKind.Implementation)
        {
            // An override is implemented by every class that writes one.
            foreach (var type in classes)
                foreach (var member in type.Children)
                    if (member.Name == name && document.Line(member.Range.Start.Line).Contains(" override ", StringComparison.Ordinal))
                        found.Add(new CodeLocation(member.SelectionRange));
            return found;
        }
        if (kind == CodeDefinitionKind.TypeDefinition)
            return TypeOf(document, classes, name, position) is { } typed ? [typed] : [];

        // The member of the class the caret is in, then a class of that name, then the member in every class.
        foreach (var type in classes)
        {
            if (!Holds(type.Range, position)) continue;
            foreach (var member in type.Children)
                if (member.Name == name) return [new CodeLocation(member.SelectionRange)];
        }
        foreach (var type in classes)
            if (type.Name == name) found.Add(new CodeLocation(type.SelectionRange));
        if (found.Count > 0) return found;
        foreach (var type in classes)
            foreach (var member in type.Children)
                if (member.Name == name) found.Add(new CodeLocation(member.SelectionRange));
        if (found.Count > 0) return found;
        return Array.IndexOf(Framework, name) >= 0 ? [InFramework(name)] : [];
    }

    public Task<IReadOnlyList<CodeLocation>> ReferencesAsync(CodeDocument document, CodePosition position,
        CancellationToken cancellation)
    {
        if (NameAt(document, position) is not { } name) return Task.FromResult<IReadOnlyList<CodeLocation>>([]);
        var found = new List<CodeLocation>();
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            var at = text.IndexOf(name, StringComparison.Ordinal);
            while (at >= 0)
            {
                var whole = (at == 0 || !CodeDocument.IsWordChar(text[at - 1]))
                    && (at + name.Length == text.Length || !CodeDocument.IsWordChar(text[at + name.Length]));
                if (whole)
                    found.Add(new CodeLocation(new CodeRange(new CodePosition(line, at), new CodePosition(line, at + name.Length))));
                at = text.IndexOf(name, at + name.Length, StringComparison.Ordinal);
            }
        }
        return Task.FromResult<IReadOnlyList<CodeLocation>>(found);
    }

    /// <summary>The type of the member named <paramref name="name"/>, by the word its declaration writes before the
    /// name: a class of the file, or one of the framework's.</summary>
    private static CodeLocation? TypeOf(CodeDocument document, List<CodeSymbol> classes, string name, CodePosition position)
    {
        foreach (var type in classes)
        {
            if (!Holds(type.Range, position)) continue;
            foreach (var member in type.Children)
            {
                if (member.Name != name || member.Kind == CodeSymbolKind.Constructor) continue;
                var line = document.Line(member.SelectionRange.Start.Line);
                var before = line.Substring(0, member.SelectionRange.Start.Column).TrimEnd();
                var typeName = before.Substring(before.LastIndexOf(' ') + 1);
                var generic = typeName.IndexOf('<');
                if (generic > 0) typeName = typeName.Substring(0, generic);
                foreach (var declared in classes)
                    if (declared.Name == typeName) return new CodeLocation(declared.SelectionRange);
                return InFramework(typeName);
            }
        }
        return null;
    }

    /// <summary>Where the framework defines <paramref name="name"/>: its own source, another document.</summary>
    private static CodeLocation InFramework(string name) =>
        new(new CodeRange(new CodePosition(0, 0))) { Document = "System/" + char.ToUpperInvariant(name[0]) + name.Substring(1) + ".cs" };

    /// <summary>The word at <paramref name="position"/>, on the character there or the one before it.</summary>
    private static string? NameAt(CodeDocument document, CodePosition position)
    {
        var line = document.Line(position.Line);
        var column = position.Column < line.Length && CodeDocument.IsWordChar(line[position.Column]) ? position.Column
            : position.Column > 0 && position.Column - 1 < line.Length && CodeDocument.IsWordChar(line[position.Column - 1]) ? position.Column - 1
            : -1;
        if (column < 0) return null;
        var word = document.WordAt(new CodePosition(position.Line, column));
        return document.TextIn(word);
    }

    private static bool Holds(CodeRange range, CodePosition position) =>
        range.Start.CompareTo(position) <= 0 && position.CompareTo(range.End) <= 0;
}
