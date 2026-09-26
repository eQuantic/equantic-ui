using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The write-once <see cref="CodeDiff"/>, live in the browser: a ledger class against a revision of
/// it, side by side or inline, whose modified side edits and is compared again as it is typed into;
/// and a change as git writes it, read from the patch alone. The same component runs on Photon.
/// </summary>
[Page("/diff", Title = "Code diff — eQuantic Console")]
public sealed class DiffScreen : StatefulComponent
{
    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    private string _status = "";

    /// <summary>Parsed once and kept: a diff opens its patch again when it is handed a new one.</summary>
    private readonly CodePatchFile _patch = CodePatch.Parse(PatchText)[0];

    private static readonly string Before = Revision(false);
    private static readonly string After = Revision(true);

    /// <summary>A class long enough that its unchanged runs fold, in two versions: a method renamed
    /// and rewritten, a guard added, a line removed and a long line changed in its middle.</summary>
    private static string Revision(bool revised)
    {
        var lines = new List<string>
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Linq;",
            "",
            "namespace Ledger.Accounting;",
            "",
            "/// <summary>One invoice in the ledger.</summary>",
            "public sealed class Invoice",
            "{",
            "    private readonly List<decimal> _amounts = new();",
            "",
        };
        for (var i = 1; i <= 12; i++) lines.Add($"    public string Field{i} {{ get; init; }} = \"\";");
        lines.Add("");
        lines.Add("    public Invoice(string reference, DateTime issuedAt)");
        lines.Add("    {");
        if (revised) lines.Add("        ArgumentException.ThrowIfNullOrWhiteSpace(reference);");
        lines.Add("        Reference = reference;");
        lines.Add("        IssuedAt = issuedAt;");
        lines.Add("    }");
        lines.Add("");
        lines.Add("    public string Reference { get; }");
        lines.Add("    public DateTime IssuedAt { get; }");
        lines.Add(revised
            ? "    public decimal Total => _amounts.Sum(amount => Math.Round(amount, 2));"
            : "    public decimal Total => _amounts.Sum();");
        lines.Add("");
        lines.Add(revised ? "    public void Charge(decimal amount)" : "    public void Add(decimal amount)");
        lines.Add("    {");
        lines.Add("        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));");
        lines.Add("        _amounts.Add(amount);");
        lines.Add("    }");
        if (!revised)
        {
            lines.Add("");
            lines.Add("    // Kept for the old importer, which nothing calls any more.");
        }
        for (var i = 1; i <= 12; i++) lines.Add($"    public int Count{i}() => _amounts.Count + {i};");
        lines.Add("");
        lines.Add(revised
            ? "    public override string ToString() => $\"Invoice {Reference} of {IssuedAt:yyyy-MM-dd}: {Total:0.00}, settled by the end of the month\";"
            : "    public override string ToString() => $\"Invoice {Reference} of {IssuedAt:yyyy-MM-dd}: {Total:0.00}, settled on receipt\";");
        lines.Add("}");
        return string.Join("\n", lines);
    }

    /// <summary>A change as <c>git diff</c> writes it: two hunks, and the lines between them that the
    /// patch leaves out.</summary>
    private const string PatchText = """
        diff --git a/src/Ledger/Payment.cs b/src/Ledger/Payment.cs
        index 3f2a1c0..9b7e4d2 100644
        --- a/src/Ledger/Payment.cs
        +++ b/src/Ledger/Payment.cs
        @@ -8,7 +8,8 @@ namespace Ledger.Accounting;
         public sealed class Payment
         {
             private readonly List<decimal> _amounts = new();
        -    public Payment(string reference) => Reference = reference;
        +    public Payment(string reference, string currency) =>
        +        (Reference, Currency) = (reference, currency);

             public string Reference { get; }

        @@ -40,4 +41,4 @@ public sealed class Payment
             public decimal Total => _amounts.Sum();

        -    public override string ToString() => $"Payment {Reference}";
        +    public override string ToString() => $"Payment {Reference} in {Currency}";
         }
        """;

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/diff", "Code diff", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(Space.S4, Space.S4, 0, Space.S4),
        }, new Text(_status.Length == 0 ? "Edit the right side: the diff follows. F7 steps through the changes." : _status,
            TypeRole.BodyM, theme.TextSecondary, maxLines: 1)));
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(0, Space.S4, 0, Space.S4),
        }, new CodeDiff(Before, After, "csharp")
        {
            MaxHeight = 460,
            OriginalCaption = "Invoice.cs (main)",
            ModifiedCaption = "Invoice.cs",
            OnChanged = text => SetState(() => _status = $"Edited: the modified side has {text.Length} characters."),
        }));
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(0, Space.S4, Space.S4, Space.S4),
        }, new CodeDiff("", "", "csharp")
        {
            Patch = _patch,
            Inline = true,
            MaxHeight = 320,
            ModifiedCaption = "src/Ledger/Payment.cs",
        }));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }
}
