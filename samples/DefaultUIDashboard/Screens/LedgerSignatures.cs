using eQuantic.UI.Code;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The code screen's signature help: the calls the ledger makes, read from the text alone. It finds the
/// call the caret is in (the innermost parenthesis still open before it on its line), the argument it is on
/// (the commas at that depth) and an argument named <c>name:</c>, as a language server would, and answers
/// nothing outside a call, which closes the panel. A real app hands its language service the same way.
/// </summary>
public sealed class LedgerSignatures : ICodeSignatureProvider
{
    private static readonly string[] Kinds = ["Invoice", "Payment", "Refund", "Transfer", "Deposit", "Withdrawal"];

    public Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
        CodeSignatureContext context, CancellationToken cancellation)
    {
        var line = document.Line(position.Line);
        var caret = Math.Min(position.Column, line.Length);
        var open = OpenParenthesis(line, caret);
        if (open < 0) return Task.FromResult<CodeSignatureHelp?>(null);
        var signatures = Known(Callee(line, open));
        if (signatures.Count == 0) return Task.FromResult<CodeSignatureHelp?>(null);

        // The argument the caret is on: the commas at the call's own depth, or the parameter an argument
        // names.
        var argument = 0;
        var from = open + 1;
        var depth = 0;
        for (var i = open + 1; i < caret; i++)
        {
            var c = line[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                argument++;
                from = i + 1;
            }
        }
        var typed = line.Substring(from, caret - from).Trim();
        var colon = typed.IndexOf(':');
        var active = argument;
        if (colon > 0)
        {
            var name = typed.Substring(0, colon).Trim();
            var parameters = signatures[0].Parameters;
            for (var p = 0; p < parameters.Count; p++)
                if (parameters[p].Label.EndsWith(" " + name, StringComparison.Ordinal)) active = p;
        }

        // The first signature with room for the argument the caret is on, as overload resolution would
        // start.
        var chosen = 0;
        for (var s = 0; s < signatures.Count; s++)
        {
            if (signatures[s].Parameters.Count <= active) continue;
            chosen = s;
            break;
        }
        return Task.FromResult<CodeSignatureHelp?>(new CodeSignatureHelp(signatures)
        {
            ActiveSignature = chosen,
            ActiveParameter = active,
        });
    }

    /// <summary>Where the innermost parenthesis still open before <paramref name="caret"/> stands, or -1.</summary>
    private static int OpenParenthesis(string line, int caret)
    {
        var depth = 0;
        for (var i = caret - 1; i >= 0; i--)
        {
            if (line[i] == ')') depth++;
            else if (line[i] == '(')
            {
                if (depth == 0) return i;
                depth--;
            }
        }
        return -1;
    }

    /// <summary>The name a call's parenthesis follows.</summary>
    private static string Callee(string line, int open)
    {
        var end = open;
        while (end > 0 && line[end - 1] == ' ') end--;
        var start = end;
        while (start > 0 && CodeDocument.IsWordChar(line[start - 1])) start--;
        return line.Substring(start, end - start);
    }

    private static List<CodeSignature> Known(string callee)
    {
        if (Array.IndexOf(Kinds, callee) >= 0)
        {
            return
            [
                new CodeSignature($"{callee}(string reference, DateTime issuedAt)")
                {
                    Documentation = $"Opens a new {callee.ToLowerInvariant()} in the ledger.",
                    Parameters =
                    [
                        new CodeSignatureParameter("string reference") { Documentation = "What the document is issued under." },
                        new CodeSignatureParameter("DateTime issuedAt") { Documentation = "When it was issued." },
                    ],
                },
            ];
        }
        return callee switch
        {
            "Add" =>
            [
                new CodeSignature("void Add(decimal amount)")
                {
                    Documentation = "Adds an amount to the document's total.",
                    Parameters = [new CodeSignatureParameter("decimal amount") { Documentation = "More than zero." }],
                },
            ],
            "ArgumentOutOfRangeException" =>
            [
                new CodeSignature("ArgumentOutOfRangeException()"),
                new CodeSignature("ArgumentOutOfRangeException(string? paramName)")
                {
                    Parameters = [new CodeSignatureParameter("string? paramName") { Documentation = "The parameter out of range." }],
                },
                new CodeSignature("ArgumentOutOfRangeException(string? paramName, string? message)")
                {
                    Parameters =
                    [
                        new CodeSignatureParameter("string? paramName") { Documentation = "The parameter out of range." },
                        new CodeSignatureParameter("string? message") { Documentation = "Why it is out of range." },
                    ],
                },
            ],
            _ => [],
        };
    }
}
