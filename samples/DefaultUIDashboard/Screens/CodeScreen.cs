using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

using eQuantic.Console;

namespace DefaultUIDashboard.Screens;

/// <summary>
/// The write-once <see cref="CodeEditor"/>, live in the browser, over a file long enough to scroll
/// in both directions. The SAME component, controller and keymap run natively on Photon (the
/// Studio's gallery), so what this screen shows is what an IDE built on the SDK gets.
/// </summary>
[Page("/code", Title = "Code editor — eQuantic Console")]
public sealed class CodeScreen : StatefulComponent
{
    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    private string _status = "";

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
            lines.Add("        _amounts.Add(amount);");
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
        }, new Text(_status.Length == 0 ? "Click into the code and type." : _status,
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
        }));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }
}
