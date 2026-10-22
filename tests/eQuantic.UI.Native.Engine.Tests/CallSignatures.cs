using eQuantic.UI.Code;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A language service's signature help in miniature, for the tests: it finds the call the caret is in (the
/// innermost parenthesis still open before it on its line), the argument it is on (the commas at that depth)
/// and a named argument (<c>gap:</c>), and answers the signatures it knows for the name before the
/// parenthesis, or nothing outside a call.
/// </summary>
internal sealed class CallSignatures(Dictionary<string, CodeSignature[]> known) : ICodeSignatureProvider
{
    public List<(CodePosition Position, CodeSignatureContext Context)> Asked { get; } = [];

    public static readonly CodeSignature Column = new("Column(float gap, MainAlign main)")
    {
        Documentation = "Lays its children out in a column.",
        Parameters = [new("float gap") { Documentation = "The space between two children." }, new("MainAlign main")],
    };

    /// <summary>Column's one signature.</summary>
    public static CallSignatures ColumnOnly() => new(new() { ["Column"] = [Column] });

    /// <summary>Add's three overloads.</summary>
    public static CallSignatures Overloaded() => new(new()
    {
        ["Add"] =
        [
            new("Add(int value)") { Parameters = [new("int value")] },
            new("Add(int value, int times)")
            {
                Documentation = "Adds a value as many times as it is told.",
                Parameters = [new("int value"), new("int times") { Documentation = "How many times to add it." }],
            },
            new("Add(string text)") { Parameters = [new("string text")] },
        ],
    });

    public Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
        CodeSignatureContext context, CancellationToken cancellation)
    {
        Asked.Add((position, context));
        return Task.FromResult(Answer(document.Line(position.Line), position.Column));
    }

    private CodeSignatureHelp? Answer(string line, int caret)
    {
        var depth = 0;
        var open = -1;
        for (var i = caret - 1; i >= 0 && open < 0; i--)
        {
            if (line[i] == ')') depth++;
            else if (line[i] == '(' && depth-- == 0) open = i;
        }
        if (open < 0) return null;
        var name = line[..open].TrimEnd();
        var start = name.Length;
        while (start > 0 && CodeDocument.IsWordChar(name[start - 1])) start--;
        if (!known.TryGetValue(name[start..], out var signatures)) return null;

        var argument = 0;
        var from = open + 1;
        depth = 0;
        for (var i = open + 1; i < caret; i++)
        {
            if (line[i] == '(') depth++;
            else if (line[i] == ')') depth--;
            else if (line[i] == ',' && depth == 0)
            {
                argument++;
                from = i + 1;
            }
        }
        var typed = line[from..caret].Trim();
        var colon = typed.IndexOf(':');
        var active = argument;
        if (colon > 0)
        {
            var named = typed[..colon].Trim();
            var parameters = signatures[0].Parameters;
            for (var p = 0; p < parameters.Count; p++)
                if (parameters[p].Label.EndsWith(" " + named, StringComparison.Ordinal)) active = p;
        }
        return new CodeSignatureHelp(signatures) { ActiveParameter = active };
    }
}
