using eQuantic.UI.Components;
using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// Recovery codes shown once, as a sign-in shows them (#1022): the codes exist only in this page, so
/// no address could serve them afterwards. "Copy all" puts them on the clipboard, and "Download .txt"
/// offers them as a file the page made, which the browser downloads and a desktop saves where the
/// person chooses.
/// </summary>
[Page("/recovery-codes", Title = "Recovery codes — eQuantic Console")]
public sealed class RecoveryCodesScreen : StatefulComponent
{
    /// <summary>The file the codes are offered as.</summary>
    public const string FileName = "recovery-codes.txt";

    private static readonly string[] Codes =
        ["7KQ2-M9XD", "P4TN-8WCE", "H6JB-2RZF", "X3LV-Q7YA", "D9GS-5KMU", "B2WH-6TPN", "R8FE-3JQC", "M5ZK-9VDL"];

    private readonly IFileSaver? _saver;
    private readonly ITextClipboard? _clipboard;
    private string _said = "";
    private bool _navOpen;

    public RecoveryCodesScreen(IFileSaver? saver = null, ITextClipboard? clipboard = null)
    {
        _saver = saver;
        _clipboard = clipboard;
    }

    /// <summary>What the file holds: one code a line, as a person would type them back.</summary>
    public static string AllCodes() => string.Join("\n", Codes) + "\n";

    private async Task Download()
    {
        var outcome = await _saver!.SaveTextAsync(FileName, AllCodes());
        SetState(() => _said = outcome switch
        {
            FileSaveOutcome.Saved => "Saved where you chose.",
            FileSaveOutcome.Cancelled => "Nothing was saved.",
            _ => $"Your browser is saving {FileName}.",
        });
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var codes = Column(gap: Space.S2, children: [.. Codes.Select(code => (VisualNode)Text(code, TypeRole.BodyL, theme.TextPrimary))]);

        return ConsoleShell.Frame(theme, "/recovery-codes", "Recovery codes",
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = theme.Background,
                Padding = EdgeInsets.All(Space.S6),
            },
            Column(gap: Space.S4, children: [
                Text("Your recovery codes", TypeRole.Heading, theme.TextPrimary),
                Text("Each code signs you in once if you lose your device. They are shown only now.",
                    TypeRole.BodyM, theme.TextSecondary),
                codes,
                Row(gap: Space.S3, children: [
                    new Button("Download .txt", onPressed: () => _ = Download()) { Disabled = _saver is not { IsAvailable: true } },
                    new Button("Copy all", Variant.Outline, onPressed: () => SetState(() =>
                    {
                        _clipboard?.Write(AllCodes());
                        _said = "Copied.";
                    })) { Disabled = _clipboard is null },
                ]),
                Text(_said, TypeRole.BodyM, theme.TextSecondary),
            ])),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));
    }
}
