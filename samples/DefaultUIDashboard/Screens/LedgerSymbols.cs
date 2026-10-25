using eQuantic.UI.Code;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// What the code screen's file declares, for ⇧⌘O to list and go to, read from the text alone: the namespace,
/// each <c>public sealed class</c> and, inside it, its constructor, its properties, its methods and its
/// fields, each ranged from the line that declares it. A language server answers the same question for a real
/// app, as LSP's <c>textDocument/documentSymbol</c>.
/// </summary>
public sealed class LedgerSymbols : ICodeSymbolProvider
{
    public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation)
    {
        var symbols = new List<CodeSymbol>();
        List<CodeSymbol>? members = null;
        string? type = null;
        var typeLine = 0;
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            var trimmed = text.Trim();
            if (trimmed.StartsWith("namespace ", StringComparison.Ordinal))
            {
                var name = trimmed.Substring("namespace ".Length).TrimEnd(';');
                symbols.Add(Symbol(document, name, CodeSymbolKind.Namespace, line, line));
                continue;
            }
            if (trimmed.StartsWith("public sealed class ", StringComparison.Ordinal))
            {
                type = trimmed.Substring("public sealed class ".Length);
                typeLine = line;
                members = [];
                continue;
            }
            if (type is null || members is null) continue;
            if (trimmed == "}" && text.StartsWith('}'))
            {
                symbols.Add(Symbol(document, type, CodeSymbolKind.Class, typeLine, line) with { Children = members });
                type = null;
                continue;
            }
            if (Member(trimmed, type) is { } member)
                members.Add(Symbol(document, member.Name, member.Kind, line, line));
        }
        return Task.FromResult<IReadOnlyList<CodeSymbol>>(symbols);
    }

    /// <summary>A member a line of a class declares, by the shape the file writes it in.</summary>
    private static (string Name, CodeSymbolKind Kind)? Member(string line, string type)
    {
        // A field is named before its initializer, or before the `;` that ends it when it has none.
        if (line.StartsWith("private readonly ", StringComparison.Ordinal))
            return NameBefore(line, line.Contains(" = ", StringComparison.Ordinal) ? " = " : ";") is { } field
                ? (field, CodeSymbolKind.Field)
                : null;
        if (!line.StartsWith("public ", StringComparison.Ordinal)) return null;
        if (line.StartsWith($"public {type}(", StringComparison.Ordinal)) return (type, CodeSymbolKind.Constructor);
        var call = line.IndexOf('(');
        var get = line.IndexOf(" { get", StringComparison.Ordinal);
        var arrow = line.IndexOf(" =>", StringComparison.Ordinal);
        var kind = get > 0 ? CodeSymbolKind.Property
            : call > 0 && (arrow < 0 || call < arrow) ? CodeSymbolKind.Method
            : arrow > 0 ? CodeSymbolKind.Property
            : (CodeSymbolKind?)null;
        var mark = get > 0 ? " { get" : kind == CodeSymbolKind.Method ? "(" : " =>";
        return kind is { } found && NameBefore(line, mark) is { } name ? (name, found) : null;
    }

    /// <summary>The word that ends right before <paramref name="mark"/>, or null when the line has no such
    /// word: the file is the person's to edit, and a line half typed is no declaration.</summary>
    private static string? NameBefore(string line, string mark)
    {
        var end = line.IndexOf(mark, StringComparison.Ordinal);
        if (end <= 0) return null;
        var start = line.LastIndexOf(' ', end - 1) + 1;
        return end > start ? line.Substring(start, end - start) : null;
    }

    /// <summary>A symbol over lines <paramref name="from"/> to <paramref name="to"/>, named where its name is
    /// written on its first line.</summary>
    private static CodeSymbol Symbol(CodeDocument document, string name, CodeSymbolKind kind, int from, int to)
    {
        var first = document.Line(from);
        var at = Math.Max(0, first.IndexOf(name, StringComparison.Ordinal));
        var named = new CodeRange(new CodePosition(from, at), new CodePosition(from, at + name.Length));
        return new CodeSymbol(name, kind, new CodeRange(new CodePosition(from, 0), new CodePosition(to, document.Line(to).Length)), named);
    }
}
