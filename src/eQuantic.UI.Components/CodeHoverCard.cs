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
    /// <summary>The widest a card grows, in columns of code; past it a line wraps.</summary>
    internal const int MaxColumns = 80;

    /// <summary>The most lines one message, signature or answer takes; past them it is cut.</summary>
    internal const int MaxLinesEach = 8;

    /// <summary>The card's frame, a hairline.</summary>
    internal const float Border = 1;

    /// <summary>A problem's severity mark: a dot this wide, before its message.</summary>
    private const float Mark = 8;

    /// <summary>
    /// The card for what <paramref name="hover"/> holds, as wide as its longest line up to
    /// <see cref="MaxColumns"/> and <paramref name="viewWidth"/> (zero while nothing has measured the
    /// view), with how wide and how tall it is: the editor places it from those. Each text is laid out in
    /// as many lines as it measures, which wrapping can only exceed, so what does not fit is cut rather
    /// than short of the card.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Build(ComponentContext context,
        CodeHoverSession hover, CodeBlock.CodeMetrics metrics, float viewWidth)
    {
        var theme = context.Theme;
        var prose = metrics.Style with { Mono = false };
        // The heights a line of each face lays out at: a text's own, and never the code's row, which
        // is taller than its type (measured: a 17dp row of 14.5dp type).
        var proseLine = prose.ScaledLineHeight(context.TypeScale);
        var codeLine = metrics.Style.ScaledLineHeight(context.TypeScale);
        var chrome = 2 * (Border + Space.S2);
        var marked = Mark + Space.S1;

        // How wide the card wants to be: its longest line, each measured in the face it is drawn in.
        var wanted = 0f;
        foreach (var problem in hover.Problems)
            wanted = MathF.Max(wanted, marked + Widest(context, Said(problem), prose));
        foreach (var answer in hover.Answers)
        {
            if (answer.Signature is { Length: > 0 } signature)
                wanted = MathF.Max(wanted, Widest(context, signature, metrics.Style));
            wanted = MathF.Max(wanted, Widest(context, answer.Text, prose));
        }
        var most = MaxColumns * metrics.ColumnWidth + chrome;
        if (viewWidth > 0) most = MathF.Min(most, viewWidth);
        // A whole pixel for the widest line: a box exactly as wide as a fractional measure loses the
        // rounding of a browser's layout, and the line it was measured for ends cut by an ellipsis.
        var width = MathF.Min(most, MathF.Ceiling(wanted) + chrome);
        var room = MathF.Max(1, width - chrome);

        var column = new Column(gap: Space.S1) { Width = SizeValue.Fill };
        var height = 2 * (Border + Space.S1);
        var parts = 0;
        foreach (var problem in hover.Problems)
        {
            var lines = LinesOf(context, Said(problem), prose, room - marked);
            column.Add(Problem(theme, problem, prose, proseLine, lines));
            height += (parts++ == 0 ? 0 : Space.S1) + lines * proseLine;
        }
        if (hover.Problems.Count > 0 && hover.Answers.Count > 0)
        {
            column.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 1, Background = theme.Border }));
            height += Space.S1 + 1;
        }
        foreach (var answer in hover.Answers)
        {
            if (answer.Signature is { Length: > 0 } signature)
            {
                var lines = LinesOf(context, signature, metrics.Style, room);
                column.Add(new Text(signature, TypeRole.LabelSmall, theme.TextPrimary, maxLines: lines)
                {
                    Mono = true,
                    StyleOverride = metrics.Style,
                });
                height += (parts++ == 0 ? 0 : Space.S1) + lines * codeLine;
            }
            if (answer.Text.Length > 0)
            {
                var lines = LinesOf(context, answer.Text, prose, room);
                column.Add(new Text(answer.Text, TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines)
                {
                    StyleOverride = prose,
                });
                height += (parts++ == 0 ? 0 : Space.S1) + lines * proseLine;
            }
        }

        VisualNode card = new Box(new BoxStyle
        {
            Width = width,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = Border,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Clip = true,
        }, column);
        if (hover.ByKeyboard) card = new LiveRegion(card);
        return (card, width, height);
    }

    /// <summary>
    /// A card that says one thing, <paramref name="said"/>, in the hover card's coat, as wide as it measures up to
    /// <see cref="MaxColumns"/> and <paramref name="viewWidth"/>: what the editor says when nothing was found where
    /// the person asked (#1084), as VS Code's message stands at the caret. The keyboard asked, so it is announced.
    /// </summary>
    internal static (VisualNode Card, float Width, float Height) Message(ComponentContext context, CodeBlock.CodeMetrics metrics,
        string said, float viewWidth)
    {
        var theme = context.Theme;
        var prose = metrics.Style with { Mono = false };
        var proseLine = prose.ScaledLineHeight(context.TypeScale);
        var chrome = 2 * (Border + Space.S2);
        var most = MaxColumns * metrics.ColumnWidth + chrome;
        if (viewWidth > 0) most = MathF.Min(most, viewWidth);
        var width = MathF.Min(most, MathF.Ceiling(Widest(context, said, prose)) + chrome);
        var lines = LinesOf(context, said, prose, MathF.Max(1, width - chrome));
        var card = new LiveRegion(new Box(new BoxStyle
        {
            Width = width,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = Border,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Clip = true,
        }, new Text(said, TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines) { StyleOverride = prose }));
        return (card, width, 2 * (Border + Space.S1) + lines * proseLine);
    }

    /// <summary>
    /// Where a card <paramref name="width"/> by <paramref name="height"/> stands: its left at
    /// <paramref name="start"/>'s, where what it explains starts (in the surface's coordinates), and its
    /// bottom at the top of that line, or its top at the bottom of the line when the view has no room
    /// above it. It never leaves the view on the left or on the right, the view being from
    /// <paramref name="viewLeft"/> across <paramref name="viewWidth"/> (zero while nothing has measured
    /// it).
    /// </summary>
    internal static (float X, float Y) Place(Rect start, float viewTop, float viewLeft, float viewWidth,
        float width, float height)
    {
        var y = start.Y - height >= viewTop ? start.Y - height : start.Y + start.Height;
        var x = start.X;
        if (viewWidth > 0) x = MathF.Min(x, viewLeft + viewWidth - width);
        x = MathF.Max(x, viewLeft);
        return (x, y);
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

    /// <summary>The widest paragraph of <paramref name="text"/> in <paramref name="style"/>.</summary>
    internal static float Widest(ComponentContext context, string text, TypeStyle style)
    {
        var widest = 0f;
        foreach (var paragraph in text.Split('\n'))
            widest = MathF.Max(widest, context.MeasureText(paragraph, style));
        return widest;
    }

    /// <summary>How many lines <paramref name="text"/> takes <paramref name="room"/> wide: at least one
    /// per paragraph, and no more than <see cref="MaxLinesEach"/>.</summary>
    internal static int LinesOf(ComponentContext context, string text, TypeStyle style, float room)
    {
        var lines = 0;
        foreach (var paragraph in text.Split('\n'))
        {
            lines += WrappedLines(context, paragraph, style, MathF.Max(1, room));
            if (lines >= MaxLinesEach) return MaxLinesEach;
        }
        return lines;
    }

    /// <summary>
    /// How many lines <paramref name="paragraph"/> wraps to <paramref name="room"/> wide, broken between
    /// words as both targets break it: a word that does not fit on the line starts the next, and a word
    /// wider than the room takes as many lines as it is wide. Dividing the paragraph's width by the room
    /// counted the lines a wrap leaves half empty as full, and cut the last of them.
    /// </summary>
    private static int WrappedLines(ComponentContext context, string paragraph, TypeStyle style, float room)
    {
        var lines = 1;
        var line = "";
        foreach (var word in paragraph.Split(' '))
        {
            var longer = line.Length == 0 ? word : line + " " + word;
            if (context.MeasureText(longer, style) <= room)
            {
                line = longer;
                continue;
            }
            if (line.Length > 0) lines++;
            line = word;
            var wide = context.MeasureText(word, style);
            if (wide > room) lines += (int)MathF.Ceiling(wide / room) - 1;
        }
        return lines;
    }
}
