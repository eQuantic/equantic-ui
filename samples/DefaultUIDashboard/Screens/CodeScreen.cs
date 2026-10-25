using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The write-once <see cref="CodeEditor"/>, live in the browser, over a file long enough to scroll
/// in both directions. The SAME component, controller and keymap run natively on Photon (the
/// Studio's gallery), so what this screen shows is what an IDE built on the SDK gets: the problems a
/// provider finds as the text changes (<see cref="LedgerLint"/>), the card that explains a name
/// (<see cref="LedgerHover"/>), the panel that explains a call being written
/// (<see cref="LedgerSignatures"/>), and the snippets it completes, walked with Tab
/// (<see cref="CodeSnippetCompletionProvider"/>).
/// </summary>
[Page("/code", Title = "Code editor — eQuantic Console")]
public sealed class CodeScreen : StatefulComponent
{
    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    private string _status = "";

    /// <summary>Kept, as a controller is kept: a provider made in a build would be a new one every
    /// build, and asked again at once.</summary>
    private readonly LedgerLint _lint = new();
    private readonly LedgerHover _hover = new();
    private readonly LedgerSignatures _signatures = new();

    /// <summary>Before the language's words and the file's, so a prefix that is also a keyword offers its
    /// snippet first: a loop, a walk over a collection, and a property whose access is a choice.</summary>
    private readonly IReadOnlyList<ICodeCompletionProvider> _completions =
    [
        new CodeSnippetCompletionProvider(
        [
            new CodeSnippetDefinition("for", "for (var ${1:i} = 0; $1 < ${2:count}; $1++)\n{\n\t$0\n}", "A for loop"),
            new CodeSnippetDefinition("foreach", "foreach (var ${1:item} in ${2:items})\n{\n\t$0\n}", "A walk over a collection"),
            new CodeSnippetDefinition("prop", "${1|public,internal,private|} ${2:decimal} ${3:Name} { get; set; }$0", "A property"),
        ]),
        new CodeKeywordCompletionProvider(),
        new CodeWordCompletionProvider(),
    ];

    private static readonly string Source = BuildSource();

    /// <summary>A C# file of a realistic length: a handful of types, one very long line, and
    /// enough lines that only a window of them fits on screen.</summary>
    private static string BuildSource()
    {
        var lines = new List<string>
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "",
            "namespace Ledger.Accounting;",
            "",
            "// A deliberately long line, so the sideways scroll has something to reveal: " +
            "the quick brown fox jumps over the lazy dog, and then over it again, and again.",
            "",
        };
        string[] names = ["Invoice", "Payment", "Refund", "Transfer", "Deposit", "Withdrawal"];
        foreach (var name in names)
        {
            lines.Add($"/// <summary>One {name.ToLowerInvariant()} in the ledger.</summary>");
            lines.Add($"public sealed class {name}");
            lines.Add("{");
            lines.Add("    private readonly List<decimal> _amounts = new();");
            lines.Add("");
            lines.Add($"    public {name}(string reference, DateTime issuedAt)");
            lines.Add("    {");
            lines.Add("        Reference = reference;");
            lines.Add("        IssuedAt = issuedAt;");
            lines.Add("    }");
            lines.Add("");
            lines.Add("    public string Reference { get; }");
            lines.Add("    public DateTime IssuedAt { get; }");
            lines.Add("    public decimal Total => _amounts.Sum();");
            lines.Add("");
            lines.Add("    public void Add(decimal amount)");
            lines.Add("    {");
            lines.Add("        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));");
            // One misspelt field, for the problems to have an error to show: fixing it takes it away.
            lines.Add(name == "Refund" ? "        _amonts.Add(amount);" : "        _amounts.Add(amount);");
            lines.Add("    }");
            lines.Add("");
            lines.Add("    /* A block comment");
            lines.Add("       that spans two lines. */");
            lines.Add($"    public override string ToString() => $\"{name} {{Reference}}: {{Total:0.00}}\";");
            lines.Add("}");
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/code", "Code editor", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var page = new Column(gap: Space.S3) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(Space.S4, Space.S4, 0, Space.S4),
        }, new Text(_status.Length == 0 ? "Rest the pointer on a name, type a call such as Add(, type prop and press Enter, or click into the code and press F8." : _status,
            TypeRole.BodyM, theme.TextSecondary, maxLines: 1)));
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, new CodeEditor(Source, "csharp")
        {
            MaxHeight = 520,
            Caption = "Ledger.cs",
            OnSelectionChanged = range => SetState(() =>
                _status = $"Ln {range.Focus.Line + 1}, Col {range.Focus.Column + 1}"),
            Completions = _completions,
            DiagnosticProviders = [_lint],
            HoverProviders = [_hover],
            SignatureProviders = [_signatures],
        }));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }
}
