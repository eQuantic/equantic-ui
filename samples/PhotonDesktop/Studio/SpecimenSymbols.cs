using eQuantic.UI.Code;

namespace eQuantic.Studio;

/// <summary>
/// What the Buttons section's C# names, for ⇧⌘O to list and go to: the component it builds, then
/// <c>Sizing</c> and each of its metrics the comment lines quote, in the order the snippet writes them. The
/// snippet is one expression, so there is nothing it declares; what it names is its outline. A language server
/// answers the same question for an app's real code, as LSP's <c>textDocument/documentSymbol</c>.
/// </summary>
public sealed class SpecimenSymbols : ICodeSymbolProvider
{
    /// <summary>The one provider the gallery's editor keeps: the editor swaps its providers only when the list
    /// it is handed holds other instances.</summary>
    public static SpecimenSymbols Instance { get; } = new();

    private const string Metrics = "Sizing";

    public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation)
    {
        var symbols = new List<CodeSymbol>();
        var metrics = new List<CodeSymbol>();
        int? metricsFrom = null, metricsTo = null;
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            if (Built(text) is { } built)
                symbols.Add(Symbol(document, built, CodeSymbolKind.Class, line, EndOfStatement(document, line)));
            foreach (var metric in Quoted(text))
            {
                if (metrics.Exists(known => known.Name == metric)) continue;
                metrics.Add(Symbol(document, metric, CodeSymbolKind.Property, line, line));
                metricsFrom ??= line;
                metricsTo = line;
            }
        }
        if (metricsFrom is { } from && metricsTo is { } to)
            symbols.Add(Symbol(document, Metrics, CodeSymbolKind.Class, from, to) with { Children = metrics });
        return Task.FromResult<IReadOnlyList<CodeSymbol>>(symbols);
    }

    /// <summary>The type a <c>new X(</c> on the line builds, or null.</summary>
    private static string? Built(string line)
    {
        var at = line.IndexOf("new ", StringComparison.Ordinal);
        if (at < 0) return null;
        var start = at + "new ".Length;
        var end = start;
        while (end < line.Length && char.IsLetterOrDigit(line[end])) end++;
        return end > start && end < line.Length && line[end] == '(' ? line.Substring(start, end - start) : null;
    }

    /// <summary>Each <c>Sizing.X</c> the line quotes, by its member's name.</summary>
    private static IEnumerable<string> Quoted(string line)
    {
        var prefix = Metrics + ".";
        for (var at = line.IndexOf(prefix, StringComparison.Ordinal); at >= 0;
             at = line.IndexOf(prefix, at + prefix.Length, StringComparison.Ordinal))
        {
            var start = at + prefix.Length;
            var end = start;
            while (end < line.Length && char.IsLetterOrDigit(line[end])) end++;
            if (end > start) yield return line.Substring(start, end - start);
        }
    }

    /// <summary>The line the statement starting on <paramref name="from"/> ends on, its <c>;</c>.</summary>
    private static int EndOfStatement(CodeDocument document, int from)
    {
        for (var line = from; line < document.LineCount; line++)
            if (document.Line(line).TrimEnd().EndsWith(';')) return line;
        return from;
    }

    /// <summary>A symbol over lines <paramref name="from"/> to <paramref name="to"/>, named where its name is
    /// first written on its first line.</summary>
    private static CodeSymbol Symbol(CodeDocument document, string name, CodeSymbolKind kind, int from, int to)
    {
        var first = document.Line(from);
        var at = Math.Max(0, first.IndexOf(name, StringComparison.Ordinal));
        var named = new CodeRange(new CodePosition(from, at), new CodePosition(from, at + name.Length));
        return new CodeSymbol(name, kind,
            new CodeRange(new CodePosition(from, 0), new CodePosition(to, document.Line(to).Length)), named);
    }
}
