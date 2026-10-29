using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The panel a <see cref="CodeEditor"/> draws while its signature help shows one
/// (<see cref="CodeSignatureHelpSession"/>): the signature's label in the code face, the argument the caret is
/// on in the accent colour and, with overloads, which one shows of how many; then that parameter's label and
/// its documentation; then the signature's documentation, muted. It keeps no state: the editor places it.
/// <para>
/// It is a polite live region on both targets, so the parameter the caret moved to is read out: its label is
/// in the panel's text, which changes as the argument does.
/// </para>
/// <para>
/// It stands as every note does (<see cref="CodeNoteBuilder.Place"/>): over the caret's line, under it when the view
/// has no room above, and on the other side of a completion list showing, or nowhere when that side has no room.
/// </para>
/// </summary>
internal static class CodeSignatureCard
{
    /// <summary>
    /// The panel for what <paramref name="session"/> shows, as wide as its longest line up to
    /// <see cref="CodeNoteBuilder.MaxColumns"/> and <paramref name="viewWidth"/> (zero while nothing has measured
    /// the view), with how wide and how tall it is: the editor places it from those.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Build(ComponentContext context,
        CodeSignatureHelpSession session, CodeBlock.CodeMetrics metrics, float viewWidth)
    {
        var theme = context.Theme;
        var signature = session.Signature!;
        var note = new CodeNoteBuilder(context, metrics, viewWidth);

        var count = session.Help!.Signatures.Count;
        var counter = count > 1 ? $"{session.ActiveSignature + 1}/{count}" : null;
        var counterWidth = counter is null ? 0 : context.MeasureText(counter, note.Prose) + Space.S2;

        var active = session.ActiveParameter;
        var parameter = active >= 0 && active < signature.Parameters.Count ? signature.Parameters[active] : null;
        var about = parameter is null ? null : Said(parameter);

        note.Want(note.Widest(signature.Label, note.Code) + counterWidth);
        if (about is not null) note.Want(note.Widest(about, note.Prose));
        if (signature.Documentation is { Length: > 0 } documentation)
            note.Want(note.Widest(documentation, note.Prose));
        var room = note.Fit();

        var labelLines = note.LinesOf(signature.Label, note.Code, room - counterWidth);
        var first = new Row(gap: Space.S2, cross: CrossAlign.Start) { Width = SizeValue.Fill };
        first.Add(new Flexible(new Text(signature.Label, TypeRole.LabelSmall, theme.TextPrimary, maxLines: labelLines)
        {
            Mono = true,
            StyleOverride = note.Code,
            Spans = Marked(theme, signature, active),
        }));
        if (counter is not null)
            first.Add(new Text(counter, TypeRole.LabelSmall, theme.TextMuted, maxLines: 1) { StyleOverride = note.Prose });
        note.Add(first, MathF.Max(labelLines * note.CodeLine, counter is null ? 0 : note.ProseLine));

        if (about is not null)
        {
            var lines = note.LinesOf(about, note.Prose, room);
            note.Add(new Text(about, TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines)
            {
                StyleOverride = note.Prose,
                Spans = Described(theme, parameter!),
            }, lines * note.ProseLine);
        }
        if (signature.Documentation is { Length: > 0 } documented)
        {
            var lines = note.LinesOf(documented, note.Prose, room);
            note.Add(new Text(documented, TypeRole.LabelSmall, theme.TextMuted, maxLines: lines) { StyleOverride = note.Prose },
                lines * note.ProseLine);
        }
        return note.Build(announced: true);
    }

    /// <summary>The label in runs: the active parameter's span in the accent colour, the rest in the ink.</summary>
    private static List<TextRun> Marked(IAppTheme theme, CodeSignature signature, int active)
    {
        var start = signature.ParameterStart(active);
        if (start < 0) return [new TextRun(signature.Label, theme.TextPrimary)];
        var end = start + signature.Parameters[active].Label.Length;
        var runs = new List<TextRun>();
        if (start > 0) runs.Add(new TextRun(signature.Label.Substring(0, start), theme.TextPrimary));
        runs.Add(new TextRun(signature.Label.Substring(start, end - start), theme.Colors(Variant.Primary).Base));
        if (end < signature.Label.Length) runs.Add(new TextRun(signature.Label.Substring(end), theme.TextPrimary));
        return runs;
    }

    /// <summary>What the panel says of a parameter: its label, and its documentation after it.</summary>
    private static string Said(CodeSignatureParameter parameter) =>
        parameter.Documentation is { Length: > 0 } documentation ? $"{parameter.Label}: {documentation}" : parameter.Label;

    private static List<TextRun> Described(IAppTheme theme, CodeSignatureParameter parameter)
    {
        var runs = new List<TextRun> { new(parameter.Label, theme.TextPrimary) };
        if (parameter.Documentation is { Length: > 0 } documentation)
            runs.Add(new TextRun($": {documentation}", theme.TextSecondary));
        return runs;
    }
}
