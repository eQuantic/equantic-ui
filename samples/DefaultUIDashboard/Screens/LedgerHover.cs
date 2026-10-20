using eQuantic.UI.Code;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// What the code screen's card says about the word the pointer rests on, read from the text alone:
/// a class the file declares, with the summary written above it, a member every class has, and the
/// framework types the file uses. A language server answers the same question for a real app.
/// </summary>
public sealed class LedgerHover : ICodeHoverProvider
{
    public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation)
    {
        var word = document.WordAt(position);
        var name = document.TextIn(word);
        var answer = name.Length == 0 ? null : Declared(document, word, name) ?? Known(word, name);
        return Task.FromResult(answer);
    }

    /// <summary>A class the file declares, as <c>public sealed class Name</c>, with its summary.</summary>
    private static CodeHover? Declared(CodeDocument document, CodeRange word, string name)
    {
        var declaration = $"public sealed class {name}";
        for (var line = 0; line < document.LineCount; line++)
        {
            if (document.Line(line).Trim() != declaration) continue;
            var summary = line > 0 ? Summary(document.Line(line - 1)) : null;
            return new CodeHover(word, summary ?? "A class of the ledger.")
            {
                Signature = $"class Ledger.Accounting.{name}",
            };
        }
        return null;
    }

    /// <summary>The text of a one-line <c>/// &lt;summary&gt;</c>, or null.</summary>
    private static string? Summary(string line)
    {
        const string open = "/// <summary>";
        const string close = "</summary>";
        var text = line.Trim();
        if (!text.StartsWith(open, StringComparison.Ordinal) || !text.EndsWith(close, StringComparison.Ordinal)) return null;
        return text.Substring(open.Length, text.Length - open.Length - close.Length);
    }

    private static CodeHover? Known(CodeRange word, string name) => name switch
    {
        "Reference" => Hover(word, "string Reference { get; }", "What the document was issued under."),
        "IssuedAt" => Hover(word, "DateTime IssuedAt { get; }", "When the document was issued."),
        "Total" => Hover(word, "decimal Total { get; }", "The sum of every amount added so far."),
        "Add" => Hover(word, "void Add(decimal amount)", "Adds an amount, which has to be more than zero."),
        "decimal" => Hover(word, "struct System.Decimal", "A decimal number, exact for money."),
        "string" => Hover(word, "class System.String", "Text, as a sequence of UTF-16 code units."),
        "DateTime" => Hover(word, "struct System.DateTime", "An instant, with no offset from UTC."),
        "List" => Hover(word, "class System.Collections.Generic.List<T>", "A list of items that grows as they are added."),
        _ => null,
    };

    private static CodeHover Hover(CodeRange word, string signature, string text) =>
        new(word, text) { Signature = signature };
}
