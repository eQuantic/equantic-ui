using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The list a <see cref="CodeEditor"/> draws while its completion shows one: where it stands over the
/// code, and what its rows say. It keeps no state. The editor holds which page of the list is in view
/// and how wide the list has grown, and asks here for the rest.
/// <para>
/// The rows are the code's own lines, the editor's line height in its code face, so a list over the
/// code reads as part of it: the entry's kind as a letter, its label with what the word matched in the
/// accent colour, its detail muted after it, and the selected entry's documentation on the side away
/// from the line once its provider has resolved it.
/// </para>
/// </summary>
internal static class CodeCompletionView
{
    /// <summary>The most rows a list shows: a page, which PageUp and PageDown step by.</summary>
    internal const int PageRows = 12;

    /// <summary>The narrowest a list is, in columns of code, so a list of short words is no sliver.</summary>
    internal const int MinColumns = 24;

    /// <summary>The widest a list grows, in columns of code: past it a long detail is cut.</summary>
    internal const int MaxColumns = 60;

    /// <summary>The most lines of documentation shown with the list.</summary>
    internal const int DocumentationLines = 4;

    /// <summary>The most of an entry's documentation that is laid out: more than four lines of the
    /// widest list hold, so a language service's page of documentation is never wrapped whole, on
    /// every build, to show its first lines.</summary>
    private const int DocumentationBudget = DocumentationLines * MaxColumns * 2;

    /// <summary>The list's frame, a hairline.</summary>
    private const float Border = 1;

    /// <summary>How wide the mark is that says where the page is in the whole list.</summary>
    private const float PageMarkWidth = 3;

    /// <summary>What documentation adds to its lines: the rule that parts it from the rows, and its
    /// padding above and below them.</summary>
    private const float DocumentationChrome = 1 + 2 * Space.S1;

    /// <summary>From the list's left edge to where its labels start: the frame, the row's padding,
    /// the letter's cell and the gap after it. The list stands this far left of the word, so its
    /// labels line up with what was typed.</summary>
    internal static float LabelInset(CodeBlock.CodeMetrics metrics) =>
        Border + Space.S2 + metrics.LineHeight + Space.S1;

    /// <summary>How many columns of code the widest of <paramref name="items"/> takes, its label and
    /// its detail with two columns between them, from <see cref="MinColumns"/> to
    /// <see cref="MaxColumns"/>.</summary>
    internal static int ColumnsOf(IReadOnlyList<CodeCompletionMatch> items)
    {
        var widest = MinColumns;
        foreach (var match in items)
        {
            var item = match.Item;
            var columns = item.Label.Length + (item.Detail is { Length: > 0 } detail ? 2 + detail.Length : 0);
            if (columns > widest) widest = columns;
        }
        return Math.Min(widest, MaxColumns);
    }

    /// <summary>How wide a list of <paramref name="columns"/> is: the inset to its labels, the
    /// columns, the row's padding after them, the page mark and the gap before it, and the frame. The
    /// mark's room is kept whether or not the whole list fits, so the list does not narrow as the word
    /// filters it down to a page.</summary>
    internal static float WidthOf(CodeBlock.CodeMetrics metrics, int columns) =>
        LabelInset(metrics) + columns * metrics.ColumnWidth + Space.S2 + Space.S1 + PageMarkWidth + Border;

    /// <summary>How tall a list of <paramref name="rows"/> is, without its documentation.</summary>
    internal static float HeightOf(CodeBlock.CodeMetrics metrics, int rows) =>
        rows * metrics.LineHeight + 2 * (Space.S1 + Border);

    /// <summary>The documentation's face: the code's size, in the text face, since it is prose.</summary>
    private static TypeStyle DocumentationStyle(CodeBlock.CodeMetrics metrics) => metrics.Style with { Mono = false };

    /// <summary>
    /// How many lines <paramref name="documentation"/> takes in a list <paramref name="width"/> wide:
    /// at least one per paragraph, and never more than <see cref="DocumentationLines"/>. An estimate
    /// from the width each paragraph measures, which wrapping can only exceed, so the text shown in
    /// that many lines is cut rather than short of its box.
    /// </summary>
    internal static int DocumentationLinesOf(ComponentContext context, CodeBlock.CodeMetrics metrics,
        string documentation, float width)
    {
        var room = MathF.Max(1, width - 2 * (Border + Space.S2));
        var style = DocumentationStyle(metrics);
        var lines = 0;
        foreach (var paragraph in Shown(documentation).Split('\n'))
        {
            lines += Math.Max(1, (int)MathF.Ceiling(context.MeasureText(paragraph, style) / room));
            // The box holds no more, and what it cannot show is not measured.
            if (lines >= DocumentationLines) return DocumentationLines;
        }
        return lines;
    }

    /// <summary>The part of <paramref name="documentation"/> that is laid out (see
    /// <see cref="DocumentationBudget"/>).</summary>
    private static string Shown(string documentation) =>
        documentation.Length > DocumentationBudget ? documentation.Substring(0, DocumentationBudget) : documentation;

    /// <summary>How tall a line of documentation is.</summary>
    internal static float DocumentationLineOf(ComponentContext context, CodeBlock.CodeMetrics metrics) =>
        DocumentationStyle(metrics).ScaledLineHeight(context.TypeScale);

    /// <summary>How tall <paramref name="lines"/> lines of documentation, each <paramref name="line"/>
    /// tall, are with the rule that parts them from the rows. Zero lines is none.</summary>
    internal static float DocumentationHeightOf(float line, int lines) =>
        lines == 0 ? 0 : DocumentationChrome + lines * line;

    /// <summary>
    /// Where a list stands, and how many of <paramref name="wanted"/> rows it shows. Its labels start
    /// at <paramref name="word"/>'s left (where the word being completed starts, in the surface's
    /// coordinates) and its top is the bottom of the word's line, or its rows end at the top of the
    /// line when a page fits only there. When it fits neither side, it takes the roomier one and as
    /// many rows as fit, never fewer than one. It never leaves the view on the left or on the right,
    /// the view being from <paramref name="viewLeft"/> across <paramref name="viewWidth"/> (zero while
    /// nothing has measured it). A line scrolled out of the view takes its list with it, below it as
    /// ever.
    /// <para>
    /// On its side away from the line the list draws the selected entry's documentation,
    /// <paramref name="documentation"/> lines each <paramref name="documentationLine"/> tall. It takes
    /// the room the rows leave on that side and no more, as many of its lines as fit or none, and a
    /// list above the line stands that much higher. The rows never yield to it: the documentation
    /// changes with the selection, and a list that moved or lost rows as the arrows walked it would
    /// jump under the reader's eye.
    /// </para>
    /// </summary>
    internal static (float X, float Y, int Rows, bool Above, int DocumentationLines) Place(
        CodeBlock.CodeMetrics metrics, Rect word, float viewTop, float viewBottom, float viewLeft,
        float viewWidth, int wanted, float width, int documentation, float documentationLine)
    {
        var below = viewBottom - (word.Y + word.Height);
        var above = word.Y - viewTop;
        var shown = word.Y + word.Height > viewTop && word.Y < viewBottom;
        var rows = wanted;
        var up = false;
        if (shown && HeightOf(metrics, wanted) > below)
        {
            if (HeightOf(metrics, wanted) <= above) up = true;
            else
            {
                up = above > below;
                var room = (up ? above : below) - HeightOf(metrics, 0);
                rows = Math.Max(1, Math.Min(wanted, (int)MathF.Floor(room / metrics.LineHeight)));
            }
        }
        var lines = documentation;
        if (shown)
        {
            var spare = (up ? above : below) - HeightOf(metrics, rows) - DocumentationChrome;
            lines = Math.Max(0, Math.Min(lines, (int)MathF.Floor(spare / documentationLine)));
        }
        var x = word.X - LabelInset(metrics);
        if (viewWidth > 0) x = MathF.Min(x, viewLeft + viewWidth - width);
        x = MathF.Max(x, viewLeft);
        var y = up
            ? word.Y - HeightOf(metrics, rows) - DocumentationHeightOf(documentationLine, lines)
            : word.Y + word.Height;
        return (x, y, rows, up, lines);
    }

    /// <summary>
    /// The list: a page of <paramref name="rows"/> entries from <paramref name="top"/>, in a frame
    /// <paramref name="width"/> wide, the selected entry washed, a mark on the right saying where the
    /// page is in the whole, and the selected entry's <paramref name="documentation"/> in
    /// <paramref name="documentationLines"/> lines on the side away from the line. A press on a row
    /// hands <paramref name="pick"/> that entry's index.
    /// </summary>
    internal static VisualNode Build(ComponentContext context, CodeCompletion completion,
        CodeBlock.CodeMetrics metrics, int top, int rows, float width, bool above, string? documentation,
        int documentationLines, Action<int> pick)
    {
        var theme = context.Theme;
        var items = completion.Items;
        // The columns a row has for its label and its detail: the list's width less its chrome.
        var columns = (int)MathF.Floor((width - WidthOf(metrics, 0)) / metrics.ColumnWidth);
        var page = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = top; i < top + rows && i < items.Count; i++)
        {
            var index = i;
            page.Add(Option(theme, metrics, items[i], i == completion.Selected, columns, () => pick(index)));
        }

        var paged = new Row(gap: Space.S1, cross: CrossAlign.Start) { Width = SizeValue.Fill };
        paged.Add(new Flexible(page));
        paged.Add(PageMark(theme, metrics, top, rows, items.Count));

        var list = new Column(gap: 0) { Width = SizeValue.Fill };
        var documented = Documentation(context, metrics, documentation, documentationLines, above);
        if (above && documented is not null) list.Add(documented);
        list.Add(paged);
        if (!above && documented is not null) list.Add(documented);

        return new Box(new BoxStyle
        {
            Width = width,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = Border,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(0, Space.S1),
            Clip = true,
        }, list);
    }

    /// <summary>
    /// One entry, as an option the keyboard never lands on: a press accepts it while the code keeps
    /// the keyboard. Its name is its label and its detail, whole, so the letter before them, which
    /// says the same as the detail, is not read out. What it draws fits its
    /// <paramref name="columns"/>: the detail is cut first and the label after it, each ending in an
    /// ellipsis, by counting the code face's columns, so both targets cut a row in the same place
    /// where each one's flex would have cut it differently, or not at all.
    /// </summary>
    private static VisualNode Option(IAppTheme theme, CodeBlock.CodeMetrics metrics, CodeCompletionMatch match,
        bool selected, int columns, Action pressed)
    {
        var item = match.Item;
        var ink = selected ? theme.Colors(Variant.Primary).OnSubtle : theme.TextPrimary;
        var label = Fit(item.Label, columns);
        var row = new Row(gap: Space.S1) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        row.Add(Glyph(theme, metrics, item.Kind));
        row.Add(new Text(label, TypeRole.LabelSmall, ink, maxLines: 1)
        {
            Mono = true,
            StyleOverride = metrics.Style,
            Spans = Marked(label, match.Highlights, ink, theme.Colors(Variant.Primary).Base),
        });
        // The detail has what the label left, less the two columns between them.
        var room = columns - label.Length - 2;
        var detail = item.Detail is { Length: > 0 } said && room >= 2 ? Fit(said, room) : null;
        if (detail is not null)
        {
            row.Add(new Flexible(new Spacer()));
            row.Add(new Text(detail, TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)
            {
                Mono = true,
                StyleOverride = metrics.Style,
            });
        }

        return new Pressable(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = metrics.LineHeight,
            Padding = EdgeInsets.Symmetric(Space.S2, 0),
            // The keyboard's entry wears the selected coat and the pointer's the hover one, as in a
            // Select's list.
            Background = selected ? theme.Colors(Variant.Primary).Subtle : null,
            Hover = selected ? null : new StyleDiff { Background = theme.SurfaceSubtle },
        }, row), pressed)
        {
            Role = PressableRole.Option,
            Selected = selected,
            CanRequestFocus = false,
            Label = item.Detail is { Length: > 0 } whole ? item.Label + ", " + whole : item.Label,
        };
    }

    /// <summary><paramref name="text"/> in no more than <paramref name="columns"/> columns of the code
    /// face, its end replaced by an ellipsis when it is longer.</summary>
    internal static string Fit(string text, int columns) =>
        text.Length <= columns ? text : columns <= 1 ? "…" : text.Substring(0, columns - 1) + "…";

    /// <summary>The entry's kind, as its letter in its colour, centred in a square cell a line tall.</summary>
    private static VisualNode Glyph(IAppTheme theme, CodeBlock.CodeMetrics metrics, CodeCompletionKind kind)
    {
        var cell = new Row(main: MainAlign.Center)
        {
            Width = SizeValue.Fixed(metrics.LineHeight),
            Height = SizeValue.Fixed(metrics.LineHeight),
        };
        cell.Add(new Text(GlyphOf(kind), TypeRole.LabelSmall, theme.Code(InkOf(kind)), maxLines: 1)
        {
            Mono = true,
            StyleOverride = metrics.Style with { Weight = FontWeight.Bold },
        });
        return cell;
    }

    /// <summary>
    /// The letter that stands for what an entry is. Kinds a code editor draws with one icon share a
    /// letter, as VS Code's icons share a glyph (what is called, what holds a value, what is a
    /// constant), and a type is a capital.
    /// </summary>
    internal static string GlyphOf(CodeCompletionKind kind) => kind switch
    {
        CodeCompletionKind.Method or CodeCompletionKind.Function or CodeCompletionKind.Constructor => "m",
        CodeCompletionKind.Field or CodeCompletionKind.Variable => "v",
        CodeCompletionKind.Property => "p",
        CodeCompletionKind.Event => "e",
        CodeCompletionKind.Class => "C",
        CodeCompletionKind.Struct => "S",
        CodeCompletionKind.Interface => "I",
        CodeCompletionKind.Enum => "E",
        CodeCompletionKind.TypeParameter => "T",
        CodeCompletionKind.Module => "N",
        CodeCompletionKind.EnumMember or CodeCompletionKind.Constant => "c",
        CodeCompletionKind.Value or CodeCompletionKind.Unit or CodeCompletionKind.Color => "#",
        CodeCompletionKind.Keyword => "k",
        CodeCompletionKind.Snippet => "s",
        CodeCompletionKind.Operator => "o",
        CodeCompletionKind.Reference => "r",
        CodeCompletionKind.File => "f",
        CodeCompletionKind.Folder => "d",
        _ => "a",
    };

    /// <summary>The letter's colour: the one the code gives what the entry names, so a type's letter
    /// is the colour of a type and a word's is the ink.</summary>
    internal static CodeTokenKind InkOf(CodeCompletionKind kind) => kind switch
    {
        CodeCompletionKind.Method or CodeCompletionKind.Function or CodeCompletionKind.Constructor
            or CodeCompletionKind.Event => CodeTokenKind.Function,
        CodeCompletionKind.Class or CodeCompletionKind.Struct or CodeCompletionKind.Interface
            or CodeCompletionKind.Enum or CodeCompletionKind.TypeParameter
            or CodeCompletionKind.Module => CodeTokenKind.Type,
        CodeCompletionKind.Property => CodeTokenKind.Property,
        CodeCompletionKind.EnumMember or CodeCompletionKind.Constant => CodeTokenKind.Constant,
        CodeCompletionKind.Value or CodeCompletionKind.Unit or CodeCompletionKind.Color => CodeTokenKind.Number,
        CodeCompletionKind.Keyword or CodeCompletionKind.Snippet => CodeTokenKind.Keyword,
        CodeCompletionKind.Operator => CodeTokenKind.Operator,
        _ => CodeTokenKind.Plain,
    };

    /// <summary><paramref name="label"/> as runs in the code's face: the characters at
    /// <paramref name="marks"/> in <paramref name="accent"/>, the rest in <paramref name="ink"/>.</summary>
    internal static IReadOnlyList<TextRun> Marked(string label, IReadOnlyList<int> marks, ColorToken ink,
        ColorToken accent)
    {
        var marked = new bool[label.Length];
        foreach (var at in marks)
        {
            if (at >= 0 && at < label.Length) marked[at] = true;
        }
        var runs = new List<TextRun>();
        var from = 0;
        for (var i = 1; i <= label.Length; i++)
        {
            if (i < label.Length && marked[i] == marked[from]) continue;
            runs.Add(new TextRun(label.Substring(from, i - from), marked[from] ? accent : ink, true));
            from = i;
        }
        return runs;
    }

    /// <summary>
    /// The column on the right that says where the page is in the whole list: a thumb as long as the
    /// page is a share of the list, as far down as the page is. Empty while the whole list shows.
    /// </summary>
    private static VisualNode PageMark(IAppTheme theme, CodeBlock.CodeMetrics metrics, int top, int rows, int count)
    {
        var track = rows * metrics.LineHeight;
        var mark = new Column(gap: 0)
        {
            Width = SizeValue.Fixed(PageMarkWidth),
            Height = SizeValue.Fixed(track),
        };
        if (count <= rows) return mark;
        var thumb = MathF.Max(Space.S2, track * rows / count);
        mark.Add(Spacer.Fixed((track - thumb) * top / (count - rows)));
        mark.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = thumb,
            Background = theme.BorderStrong,
            CornerRadius = new CornerRadii(PageMarkWidth / 2),
        }));
        return mark;
    }

    /// <summary>
    /// The selected entry's documentation, in <paramref name="lines"/> lines of plain text, parted
    /// from the rows by a rule on the rows' side. Null when there is none to show.
    /// </summary>
    private static VisualNode? Documentation(ComponentContext context, CodeBlock.CodeMetrics metrics,
        string? documentation, int lines, bool above)
    {
        if (lines == 0 || documentation is not { Length: > 0 } text) return null;
        var theme = context.Theme;
        var rule = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 1, Background = theme.Border });
        // A box of the height the lines were counted at, so a list above the line ends on it exactly.
        var body = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = DocumentationHeightOf(DocumentationLineOf(context, metrics), lines) - 1,
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Clip = true,
        }, new Text(Shown(text), TypeRole.LabelSmall, theme.TextSecondary, maxLines: lines)
        {
            StyleOverride = DocumentationStyle(metrics),
        });
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        if (above)
        {
            column.Add(body);
            column.Add(rule);
        }
        else
        {
            column.Add(rule);
            column.Add(body);
        }
        return column;
    }
}
