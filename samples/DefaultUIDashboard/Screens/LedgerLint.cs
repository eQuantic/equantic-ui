using eQuantic.UI.Code;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The code screen's problems: four rules read from the text alone, so the sample needs no compiler.
/// An app hands its real ones the same way (a language server, a build, an analyzer run), and the
/// editor asks again whenever typing pauses, so fixing what this found takes its wave away.
/// <list type="bullet">
/// <item>An error: a field used and never declared, as the compiler names it (CS0103).</item>
/// <item>A warning: a <c>DateTime</c> property, which carries no offset.</item>
/// <item>Information: a line longer than 120 characters.</item>
/// <item>A hint: a <c>new()</c> that a collection expression says better (IDE0028).</item>
/// </list>
/// </summary>
public sealed class LedgerLint : ICodeDiagnosticProvider
{
    private const int LongestLine = 120;

    public Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation)
    {
        var found = new List<CodeDiagnostic>();
        var declared = new HashSet<string>();
        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            var underscore = text.IndexOf(" _", StringComparison.Ordinal);
            if (Declares(text) && underscore >= 0 && NameAt(text, underscore + 1) is { Length: > 1 } field)
                declared.Add(field);
        }

        for (var line = 0; line < document.LineCount; line++)
        {
            var text = document.Line(line);
            if (text.Length > LongestLine)
            {
                found.Add(new CodeDiagnostic(Range(line, LongestLine, text.Length),
                    $"Line is {text.Length} characters long, past {LongestLine}.", CodeDiagnosticSeverity.Information)
                {
                    Code = "LL0002",
                    Source = "ledger",
                });
            }
            var property = text.IndexOf("public DateTime ", StringComparison.Ordinal);
            if (property >= 0)
            {
                var type = property + "public ".Length;
                found.Add(new CodeDiagnostic(Range(line, type, type + "DateTime".Length),
                    "Prefer DateTimeOffset: a DateTime carries no offset, and a ledger is read in every time zone.",
                    CodeDiagnosticSeverity.Warning)
                {
                    Code = "LL0001",
                    Source = "ledger",
                });
            }
            var created = text.IndexOf("= new();", StringComparison.Ordinal);
            if (created >= 0)
            {
                found.Add(new CodeDiagnostic(Range(line, created + 2, created + "= new()".Length),
                    "Collection initialization can be simplified: []", CodeDiagnosticSeverity.Hint)
                {
                    Code = "IDE0028",
                    Source = "csharp",
                });
            }
            if (!Declares(text))
            {
                for (var at = text.IndexOf('_'); at >= 0; at = text.IndexOf('_', at + 1))
                {
                    if (at > 0 && IsNamePart(text[at - 1])) continue;
                    var name = NameAt(text, at);
                    if (name.Length < 2 || declared.Contains(name)) continue;
                    found.Add(new CodeDiagnostic(Range(line, at, at + name.Length),
                        $"The name '{name}' does not exist in the current context.")
                    {
                        Code = "CS0103",
                        Source = "csharp",
                    });
                    at += name.Length - 1;
                }
            }
        }
        return Task.FromResult<IReadOnlyList<CodeDiagnostic>>(found);
    }

    /// <summary>Whether <paramref name="text"/> declares a field.</summary>
    private static bool Declares(string text) => text.TrimStart().StartsWith("private ", StringComparison.Ordinal);

    private static CodeRange Range(int line, int from, int to) =>
        new(new CodePosition(line, from), new CodePosition(line, to));

    /// <summary>The name that starts at <paramref name="start"/>, or an empty one.</summary>
    private static string NameAt(string text, int start)
    {
        if (start < 0) return "";
        var end = start;
        while (end < text.Length && IsNamePart(text[end])) end++;
        return text.Substring(start, end - start);
    }

    private static bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
