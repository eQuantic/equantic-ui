using System.Runtime.CompilerServices;
using System.Text;
using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What every key does to an editor, pinned: each key an editor answers, under every combination of the
/// four modifiers, in both keyboard conventions, from the states that change what a key means (a list
/// showing, a card, the signature panel, a chord half typed, a read-only view, the trap on Tab released,
/// no clipboard). A case is written down when the editor claims the key or the key changes something, and
/// every case left out must claim nothing and change nothing.
/// <para>
/// The keymap is data (#1010), and this is what keeps the table honest: the hand-written keymap the table
/// replaced wrote the first baseline, so a binding missing from the table, or one too many, is a line that
/// moved. A change to what a key means is made on purpose, by regenerating with
/// <c>EQ_UPDATE_KEYMAP_BASELINE=1</c> and reading the diff, which is the review of that change.
/// </para>
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeKeymapBehaviourTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeKeymapBehaviourTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private const string Text = "alpha beta\n    gamma delta\nepsilon zeta";

    private static readonly string[] Keys =
    [
        "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown",
        "Enter", "Backspace", "Delete", "Tab", "Escape", " ", "a", "z", "y", "c", "x", "v", "/", "Slash",
        "k", "i", "j", "F8", "q", "Shift", "g", "o", "-", "_", "Minus",
    ];

    private sealed class Clipboard(string? text) : ITextClipboard
    {
        public string? Text { get; private set; } = text;

        public string? Read() => Text;

        public void Write(string text) => Text = text;
    }

    /// <summary>Explains every word it is asked about, at once.</summary>
    private sealed class Explainer : ICodeHoverProvider
    {
        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation) =>
            Task.FromResult<CodeHover?>(new CodeHover(document.WordAt(position), "explained"));
    }

    /// <summary>One state an editor is put in before the key, and the clipboard the key may use.</summary>
    private sealed record State(string Name, Func<(CodeEditorController Editor, Clipboard? Clipboard)> Make);

    private static CodeEditorController Editor(CodePosition caret)
    {
        var editor = new CodeEditorController(Text, CodeLanguages.CSharp);
        editor.Diagnostics.Delay = TimeSpan.Zero;
        editor.Hover.Delay = TimeSpan.Zero;
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.Selection = new CodeRange(caret);
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers,
        KeyboardConvention convention, ITextClipboard? clipboard) =>
        editor.HandleKey(key, modifiers, convention, clipboard);

    private static IEnumerable<State> States(KeyboardConvention convention)
    {
        yield return new State("plain", () => (Editor(new CodePosition(1, 6)), new Clipboard("PASTED")));
        yield return new State("selection", () =>
        {
            var editor = Editor(new CodePosition(1, 4));
            editor.Selection = new CodeRange(new CodePosition(1, 4), new CodePosition(1, 9));
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("no-clipboard", () =>
        {
            var editor = Editor(new CodePosition(1, 4));
            editor.Selection = new CodeRange(new CodePosition(1, 4), new CodePosition(1, 9));
            return (editor, null);
        });
        yield return new State("read-only", () =>
        {
            var editor = Editor(new CodePosition(1, 6));
            editor.ReadOnly = true;
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("released", () =>
        {
            var editor = Editor(new CodePosition(1, 6));
            Press(editor, "Escape", KeyModifiers.None, convention, null);
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("list", () =>
        {
            var editor = Editor(new CodePosition(2, 12));
            editor.Completion.Providers.Add(new ListCompletionProvider(
                new CodeCompletionItem("gamma"), new CodeCompletionItem("gammas"), new CodeCompletionItem("gammon")));
            editor.HandleText(" ");
            editor.HandleText("g");
            editor.HandleText("a");
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("card", () =>
        {
            var editor = Editor(new CodePosition(1, 6));
            editor.Hover.Providers.Add(new Explainer());
            editor.Hover.ShowAt(editor.Caret);
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("panel", () =>
        {
            var editor = Editor(new CodePosition(2, 12));
            editor.SignatureHelp.Providers.Add(CallSignatures.Overloaded());
            foreach (var c in " x.Add(") editor.HandleText(c.ToString());
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("problems", () =>
        {
            var editor = Editor(new CodePosition(0, 0));
            editor.Diagnostics.Report(
            [
                new CodeDiagnostic(new CodeRange(new CodePosition(1, 4), new CodePosition(1, 9)), "unused"),
                new CodeDiagnostic(new CodeRange(new CodePosition(2, 8), new CodePosition(2, 12)), "misspelt"),
            ]);
            return (editor, new Clipboard("PASTED"));
        });
        // A jump made, so the walk back has a place to go to; and walked back, so the walk forward has one.
        yield return new State("jumped", () =>
        {
            var editor = Editor(new CodePosition(0, 0));
            editor.GoTo.Open(CodeGoToMode.Line);
            editor.GoTo.Query = ":3";
            editor.GoTo.Accept();
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("walked-back", () =>
        {
            var editor = Editor(new CodePosition(0, 0));
            editor.GoTo.Open(CodeGoToMode.Line);
            editor.GoTo.Query = ":3";
            editor.GoTo.Accept();
            editor.JumpList.Back();
            return (editor, new Clipboard("PASTED"));
        });
        yield return new State("chord", () =>
        {
            var editor = Editor(new CodePosition(1, 6));
            editor.Hover.Providers.Add(new Explainer());
            Press(editor, "k", KeyModifiers.Command, convention, null);
            return (editor, new Clipboard("PASTED"));
        });
    }

    private static string Describe(CodeRange range) =>
        $"{range.Anchor.Line}:{range.Anchor.Column}>{range.Focus.Line}:{range.Focus.Column}";

    /// <summary>Everything a key can change, field by field.</summary>
    private static List<(string Name, string Value)> Capture(CodeEditorController editor, Clipboard? clipboard) =>
    [
        ("text", editor.Document.Text.Replace("\n", "\\n", StringComparison.Ordinal)),
        ("selection", Describe(editor.Selection)),
        ("list", editor.Completion.IsOpen ? $"{editor.Completion.Selected}" : "-"),
        ("card", editor.Hover.IsOpen ? "open" : "-"),
        ("panel", editor.SignatureHelp.IsOpen ? $"{editor.SignatureHelp.ActiveSignature}" : "-"),
        ("trap", editor.TabMovesFocus ? "released" : "armed"),
        ("clipboard", clipboard?.Text ?? "-"),
        ("goto", editor.GoTo.IsOpen ? editor.GoTo.Query : "-"),
    ];

    /// <summary>The fields that differ, as "name=value" pairs, or "unchanged".</summary>
    private static string Changes(List<(string Name, string Value)> before, List<(string Name, string Value)> after)
    {
        var changed = new List<string>();
        for (var i = 0; i < after.Count; i++)
            if (after[i].Value != before[i].Value) changed.Add($"{after[i].Name}={after[i].Value}");
        return changed.Count == 0 ? "unchanged" : string.Join(" ", changed);
    }

    private static string Modifiers(KeyModifiers modifiers)
    {
        if (modifiers == KeyModifiers.None) return "-";
        var parts = new List<string>();
        if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((modifiers & KeyModifiers.Command) != 0) parts.Add("Command");
        if ((modifiers & KeyModifiers.Control) != 0) parts.Add("Control");
        if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        return string.Join("+", parts);
    }

    /// <summary>
    /// Every case, in a fixed order: the convention, the state, the key and the modifiers, then what the
    /// key did. A case where the editor claimed nothing and nothing changed is left out.
    /// </summary>
    private static List<string> Behaviour()
    {
        var lines = new List<string>();
        KeyModifiers[] combinations =
        [
            .. Enumerable.Range(0, 16).Select(bits =>
                ((bits & 1) != 0 ? KeyModifiers.Shift : KeyModifiers.None)
                | ((bits & 2) != 0 ? KeyModifiers.Command : KeyModifiers.None)
                | ((bits & 4) != 0 ? KeyModifiers.Control : KeyModifiers.None)
                | ((bits & 8) != 0 ? KeyModifiers.Alt : KeyModifiers.None)),
        ];
        foreach (var convention in new[] { KeyboardConvention.Apple, KeyboardConvention.Standard })
        {
            foreach (var state in States(convention))
            {
                foreach (var key in Keys)
                {
                    foreach (var modifiers in combinations)
                    {
                        var (editor, clipboard) = state.Make();
                        var before = Capture(editor, clipboard);
                        var claimed = Press(editor, key, modifiers, convention, clipboard);
                        var changes = Changes(before, Capture(editor, clipboard));
                        if (!claimed && changes == "unchanged") continue;
                        var shown = key == " " ? "Space" : key;
                        lines.Add($"{convention} {state.Name} {shown} {Modifiers(modifiers)} => "
                            + $"{(claimed ? "claimed" : "unclaimed")} {changes}");
                    }
                }
            }
        }
        return lines;
    }

    private static string BaselinePath([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(source)!, "Coverage", "code-keymap.baseline.txt");

    [Fact]
    public void EveryKey_DoesWhatTheBaselineSays()
    {
        var actual = string.Join("\n", Behaviour()) + "\n";
        var path = BaselinePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_KEYMAP_BASELINE") == "1")
        {
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).Should().BeTrue("the baseline is written once with EQ_UPDATE_KEYMAP_BASELINE=1");
        var expected = File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n');
        var lines = actual.Split('\n');
        var missing = expected.Except(lines).Where(line => line.Length > 0).Take(12).ToList();
        var extra = lines.Except(expected).Where(line => line.Length > 0).Take(12).ToList();
        var report = new StringBuilder();
        foreach (var line in missing) report.Append("\n  baseline only: ").Append(line);
        foreach (var line in extra) report.Append("\n  keymap only:   ").Append(line);
        (missing.Count + extra.Count).Should().Be(0,
            $"every key does what the baseline says; a change made on purpose is regenerated with EQ_UPDATE_KEYMAP_BASELINE=1 and its diff read:{report}");
    }
}
