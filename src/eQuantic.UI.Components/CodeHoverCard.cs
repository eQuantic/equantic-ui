using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The card a <see cref="CodeEditor"/> draws while its hover shows one (<see cref="CodeHoverSession"/>):
/// the problems reported where it was asked, the worst first, each with its severity's mark, its message
/// and, muted after it, its rule and its source; then what the providers answered, a signature in the
/// code face and its text. It keeps no state: the editor places it, and asks here for the rest.
/// <para>
/// A card the keyboard asked for (F8, ⌘K ⌘I) is a polite live region, so a screen reader reads what the
/// key brought up; one the pointer asked for is not, since a pointer resting on code asks nobody to speak.
/// </para>
/// </summary>
internal static class CodeHoverCard
{
    /// <summary>A problem's severity mark: a dot this wide, before its message.</summary>
    private const float Mark = 8;

    /// <summary>
    /// The card for what <paramref name="hover"/> holds, as wide as its longest line up to
    /// <see cref="CodeNoteBuilder.MaxColumns"/> and <paramref name="viewWidth"/> (zero while nothing has measured the
    /// view), with how wide and how tall it is: the editor places it from those. Each text is laid out in
    /// as many lines as it measures, which wrapping can only exceed, so what does not fit is cut rather
    /// than short of the card.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Build(ComponentContext context,
        CodeHoverSession hover, CodeBlock.CodeMetrics metrics, float viewWidth)
    {
        var theme = context.Theme;
        var note = new CodeNoteBuilder(context, metrics, viewWidth);
        var marked = Mark + Space.S1;

        // How wide the card wants to be: its longest line, each measured in the face it is drawn in.
        foreach (var problem in hover.Problems)
            note.Want(marked + note.Widest(Said(problem), note.Prose));
        foreach (var answer in hover.Answers)
        {
            if (answer.Signature is { Length: > 0 } signature)
                note.Want(note.Widest(signature, note.Code));
            note.Want(note.Widest(answer.Text, note.Prose));
        }
        var room = note.Fit();

        foreach (var problem in hover.Problems)
        {
            var lines = note.LinesOf(Said(problem), note.Prose, room - marked);
            note.Add(Problem(theme, problem, note.Prose, note.ProseLine, lines), lines * note.ProseLine);
        }
        if (hover.Problems.Count > 0 && hover.Answers.Count > 0)
            note.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 1, Background = theme.Border }), 1);
        foreach (var answer in hover.Answers)
        {
            if (answer.Signature is { Length: > 0 } signature)
            {
                var lines = note.LinesOf(signature, note.Code, room);
                note.Add(new Text(signature, TypeRole.LabelSmall, theme.TextPrimary, maxLines: lines)
                {
                    Mono = true,
                    StyleOverride = note.Code,
                }, lines * note.CodeLine);
            }
            if (answer.Text.Length > 0)
            {
                var lines = note.LinesOf(answer.Text, note.Prose, room);
                note.Add(new Text(answer.Text, TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines)
                {
                    StyleOverride = note.Prose,
                }, lines * note.ProseLine);
            }
        }
        return note.Build(announced: hover.ByKeyboard);
    }

    /// <summary>
    /// A card that says one thing, <paramref name="said"/>, in the hover card's coat, as wide as it measures up to
    /// <see cref="CodeNoteBuilder.MaxColumns"/> and <paramref name="viewWidth"/>: what the editor says when nothing
    /// was found where the person asked (#1084), as VS Code's message stands at the caret. The keyboard asked, so it
    /// is announced.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Message(ComponentContext context, CodeBlock.CodeMetrics metrics,
        string said, float viewWidth)
    {
        var note = new CodeNoteBuilder(context, metrics, viewWidth);
        note.Want(note.Widest(said, note.Prose));
        var lines = note.LinesOf(said, note.Prose, note.Fit());
        return note.Frame(new Text(said, TypeRole.LabelSmall, context.Theme.TextSecondary, maxLines: lines)
        {
            StyleOverride = note.Prose,
        }, lines * note.ProseLine, announced: true);
    }

    /// <summary>What a problem says: its message, then its rule and its source when it has them.</summary>
    private static string Said(CodeDiagnostic problem) =>
        Origin(problem) is { } origin ? $"{problem.Message}  {origin}" : problem.Message;

    /// <summary>The rule and the source a problem names, as one muted run, or null.</summary>
    private static string? Origin(CodeDiagnostic problem)
    {
        var code = problem.Code is { Length: > 0 } rule ? rule : null;
        var source = problem.Source is { Length: > 0 } by ? by : null;
        if (code is not null && source is not null) return $"{source} {code}";
        return code ?? source;
    }

    private static VisualNode Problem(IAppTheme theme, CodeDiagnostic problem, TypeStyle prose, float proseLine,
        int lines)
    {
        var runs = new List<TextRun> { new(problem.Message, theme.TextPrimary) };
        if (Origin(problem) is { } origin) runs.Add(new TextRun($"  {origin}", theme.TextMuted));
        var row = new Row(gap: Space.S1, cross: CrossAlign.Start) { Width = SizeValue.Fill };
        // The dot sits on the first line's middle, however many lines the message wraps to.
        row.Add(new Box(new BoxStyle
        {
            Width = Mark,
            Padding = new EdgeInsets(0, MathF.Max(0, (proseLine - Mark) / 2), 0, 0),
        }, new Box(new BoxStyle
        {
            Width = Mark,
            Height = Mark,
            CornerRadius = new CornerRadii(Mark / 2),
            Background = SeverityColor(theme, problem.Severity),
        })));
        row.Add(new Flexible(new Text(Said(problem), TypeRole.LabelSmall, theme.TextPrimary, maxLines: lines)
        {
            StyleOverride = prose,
            Spans = runs,
        }));
        return row;
    }

    /// <summary>The colour a problem of <paramref name="severity"/> is drawn in: its wave, its dot and
    /// its mark in the gutter. A hint is muted, since it says something without calling it wrong.</summary>
    internal static ColorToken SeverityColor(IAppTheme theme, CodeDiagnosticSeverity severity) => severity switch
    {
        CodeDiagnosticSeverity.Error => theme.Colors(Variant.Destructive).Base,
        CodeDiagnosticSeverity.Warning => theme.Colors(Variant.Warning).Base,
        CodeDiagnosticSeverity.Information => theme.Colors(Variant.Info).Base,
        _ => theme.TextMuted,
    };
}
