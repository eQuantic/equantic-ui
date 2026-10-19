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
/// </summary>
internal static class CodeSignatureCard
{
    /// <summary>
    /// The panel for what <paramref name="session"/> shows, as wide as its longest line up to
    /// <see cref="CodeHoverCard.MaxColumns"/> and <paramref name="viewWidth"/> (zero while nothing has measured
    /// the view), with how wide and how tall it is: the editor places it from those.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Build(ComponentContext context,
        CodeSignatureHelpSession session, CodeBlock.CodeMetrics metrics, float viewWidth)
    {
        var theme = context.Theme;
        var signature = session.Signature!;
        var prose = metrics.Style with { Mono = false };
        var proseLine = prose.ScaledLineHeight(context.TypeScale);
        var codeLine = metrics.Style.ScaledLineHeight(context.TypeScale);
        var chrome = 2 * (CodeHoverCard.Border + Space.S2);

        var count = session.Help!.Signatures.Count;
        var counter = count > 1 ? $"{session.ActiveSignature + 1}/{count}" : null;
        var counterWidth = counter is null ? 0 : context.MeasureText(counter, prose) + Space.S2;

        var active = session.ActiveParameter;
        var parameter = active >= 0 && active < signature.Parameters.Count ? signature.Parameters[active] : null;
        var about = parameter is null ? null : Said(parameter);

        var wanted = CodeHoverCard.Widest(context, signature.Label, metrics.Style) + counterWidth;
        if (about is not null) wanted = MathF.Max(wanted, CodeHoverCard.Widest(context, about, prose));
        if (signature.Documentation is { Length: > 0 } documentation)
            wanted = MathF.Max(wanted, CodeHoverCard.Widest(context, documentation, prose));
        var most = CodeHoverCard.MaxColumns * metrics.ColumnWidth + chrome;
        if (viewWidth > 0) most = MathF.Min(most, viewWidth);
        var width = MathF.Min(most, wanted + chrome);
        var room = MathF.Max(1, width - chrome);

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        var height = 2 * (CodeHoverCard.Border + Space.S1);

        var labelLines = CodeHoverCard.LinesOf(context, signature.Label, metrics.Style, room - counterWidth);
        var first = new Row(gap: Space.S2, cross: CrossAlign.Start) { Width = SizeValue.Fill };
        first.Add(new Flexible(new Text(signature.Label, TypeRole.LabelSmall, theme.TextPrimary, maxLines: labelLines)
        {
            Mono = true,
            StyleOverride = metrics.Style,
            Spans = Marked(theme, signature, active),
        }));
        if (counter is not null)
            first.Add(new Text(counter, TypeRole.LabelSmall, theme.TextMuted, maxLines: 1) { StyleOverride = prose });
        column.Add(first);
        height += MathF.Max(labelLines * codeLine, counter is null ? 0 : proseLine);

        if (about is not null)
        {
            var lines = CodeHoverCard.LinesOf(context, about, prose, room);
            column.Add(new Text(about, TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines)
            {
                StyleOverride = prose,
                Spans = Described(theme, parameter!),
            });
            height += Space.S1 + lines * proseLine;
        }
        if (signature.Documentation is { Length: > 0 } documented)
        {
            var lines = CodeHoverCard.LinesOf(context, documented, prose, room);
            column.Add(new Text(documented, TypeRole.LabelSmall, theme.TextMuted, maxLines: lines) { StyleOverride = prose });
            height += Space.S1 + lines * proseLine;
        }

        VisualNode card = new Box(new BoxStyle
        {
            Width = width,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = CodeHoverCard.Border,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Clip = true,
        }, column);
        return (new LiveRegion(card), width, height);
    }

    /// <summary>
    /// Where a panel <paramref name="width"/> by <paramref name="height"/> stands, its left edge at the
    /// caret's and inside the view, the view being from <paramref name="viewLeft"/> across
    /// <paramref name="viewWidth"/> (zero while nothing has measured it): over the caret's line, or under it
    /// when the view has no room above. With a list showing (<paramref name="listAbove"/> says on which side
    /// of the line), on the other side, and nowhere, null, when that side has no room.
    /// </summary>
    internal static Point? Place(Rect caret, float viewTop, float viewBottom, float viewLeft, float viewWidth,
        float width, float height, bool? listAbove)
    {
        var above = caret.Y - height;
        var below = caret.Y + caret.Height;
        var roomAbove = above >= viewTop;
        var roomBelow = below + height <= viewBottom;
        float y;
        if (listAbove is null) y = roomAbove ? above : below;
        else if (listAbove == false)
        {
            if (!roomAbove) return null;
            y = above;
        }
        else
        {
            if (!roomBelow) return null;
            y = below;
        }
        var x = caret.X;
        if (viewWidth > 0) x = MathF.Min(x, viewLeft + viewWidth - width);
        x = MathF.Max(x, viewLeft);
        return new Point(x, y);
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
