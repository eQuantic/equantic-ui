using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// What every note a <see cref="CodeEditor"/> draws over its code is built with: the hover card
/// (<see cref="CodeHoverCard"/>), the signature panel (<see cref="CodeSignatureCard"/>), and any note after them. A
/// note says here what it is made of and holds the words; the builder keeps the rest: the two faces it measures
/// in, its width, its parts stacked and how tall they stand, its frame, and where it stands over the code.
/// <para>
/// A note is built in this order: it asks for each of its lines (<see cref="Want"/>), it fits (<see cref="Fit"/>),
/// which settles its width and the room its parts have, and it adds its parts (<see cref="Add"/>) before it is
/// built (<see cref="Build"/>). Its width is its longest line rounded up to a whole pixel: a box exactly as wide as
/// a fractional measure loses the rounding of a browser's layout, and the line it was measured for ends cut by an
/// ellipsis. A note built here cannot forget that pixel, which is why there is one builder and not a copy per note.
/// </para>
/// </summary>
internal sealed class CodeNoteBuilder
{
    /// <summary>The widest a note grows, in columns of code; past it a line wraps.</summary>
    internal const int MaxColumns = 80;

    /// <summary>The most lines one text of a note takes; past them it is cut.</summary>
    private const int MaxLinesEach = 8;

    /// <summary>A note's frame, a hairline.</summary>
    private const float Border = 1;

    /// <summary>The frame's padding on either side of what the note says.</summary>
    private const float PaddingAcross = Space.S2;

    /// <summary>The frame's padding above and below what the note says.</summary>
    private const float PaddingDown = Space.S1;

    /// <summary>The gap between one part of a note and the next.</summary>
    private const float Gap = Space.S1;

    /// <summary>What the frame takes of a note's width: its hairline and its padding, on both sides.</summary>
    private const float Across = 2 * (Border + PaddingAcross);

    /// <summary>What the frame takes of a note's height: its hairline and its padding, above and below.</summary>
    private const float Down = 2 * (Border + PaddingDown);

    private readonly ComponentContext _context;
    private readonly float _most;
    private readonly Column _stack;
    private float _wanted;
    private float _width;
    private float _height;
    private int _parts;

    /// <summary>
    /// A note of an editor drawn on <paramref name="metrics"/>, in a view <paramref name="viewWidth"/> wide (zero
    /// while nothing has measured it), which it never outgrows, nor <see cref="MaxColumns"/> columns of code.
    /// </summary>
    internal CodeNoteBuilder(ComponentContext context, CodeBlock.CodeMetrics metrics, float viewWidth)
    {
        _context = context;
        Code = metrics.Style;
        Prose = metrics.Style with { Mono = false };
        // The heights a line of each face lays out at: a text's own, and never the code's row, which
        // is taller than its type (measured: a 17dp row of 14.5dp type).
        CodeLine = Code.ScaledLineHeight(context.TypeScale);
        ProseLine = Prose.ScaledLineHeight(context.TypeScale);
        _most = MaxColumns * metrics.ColumnWidth + Across;
        if (viewWidth > 0) _most = MathF.Min(_most, viewWidth);
        _stack = new Column(gap: Gap) { Width = SizeValue.Fill };
        _height = Down;
    }

    /// <summary>The code face, the editor's own: what a note quotes of the code is set in it.</summary>
    internal TypeStyle Code { get; }

    /// <summary>The face of what a note says in words: the code's size, in the proportional face.</summary>
    internal TypeStyle Prose { get; }

    /// <summary>How tall a line of <see cref="Code"/> lays out.</summary>
    internal float CodeLine { get; }

    /// <summary>How tall a line of <see cref="Prose"/> lays out.</summary>
    internal float ProseLine { get; }

    /// <summary>Asks for a line <paramref name="wide"/> across: the note is as wide as the widest it was asked for.</summary>
    internal void Want(float wide) => _wanted = MathF.Max(_wanted, wide);

    /// <summary>
    /// Settles the note's width from the lines it was asked for, and answers the room its parts have across. Every
    /// <see cref="Want"/> comes before it, and every <see cref="Add"/> after.
    /// </summary>
    internal float Fit()
    {
        // A whole pixel for the widest line: a box exactly as wide as a fractional measure loses the
        // rounding of a browser's layout, and the line it was measured for ends cut by an ellipsis.
        _width = MathF.Min(_most, MathF.Ceiling(_wanted) + Across);
        return MathF.Max(1, _width - Across);
    }

    /// <summary>
    /// Stacks <paramref name="part"/> under the parts before it, <paramref name="tall"/> high: the gap above it
    /// and its height are the note's from now on.
    /// </summary>
    internal void Add(VisualNode part, float tall)
    {
        _stack.Add(part);
        _height += (_parts++ == 0 ? 0 : Gap) + tall;
    }

    /// <summary>
    /// The note: its parts stacked in its frame, a polite live region when it is <paramref name="announced"/>, with
    /// how wide and how tall it is, which the editor places it from.
    /// </summary>
    internal (VisualNode Card, float Width, float Height) Build(bool announced) => Framed(_stack, _height, announced);

    /// <summary>
    /// The note round one <paramref name="part"/> as it is, <paramref name="tall"/> high, with nothing stacked: a
    /// note that says one thing.
    /// </summary>
    internal (VisualNode Card, float Width, float Height) Frame(VisualNode part, float tall, bool announced) =>
        Framed(part, Down + tall, announced);

    /// <summary>The widest paragraph of <paramref name="text"/> in <paramref name="style"/>.</summary>
    internal float Widest(string text, TypeStyle style)
    {
        var widest = 0f;
        foreach (var paragraph in text.Split('\n'))
            widest = MathF.Max(widest, _context.MeasureText(paragraph, style));
        return widest;
    }

    /// <summary>How many lines <paramref name="text"/> takes <paramref name="room"/> wide: at least one
    /// per paragraph, and no more than <see cref="MaxLinesEach"/>.</summary>
    internal int LinesOf(string text, TypeStyle style, float room)
    {
        var lines = 0;
        foreach (var paragraph in text.Split('\n'))
        {
            lines += WrappedLines(paragraph, style, MathF.Max(1, room));
            if (lines >= MaxLinesEach) return MaxLinesEach;
        }
        return lines;
    }

    /// <summary>
    /// Where a note <paramref name="width"/> by <paramref name="height"/> stands over <paramref name="line"/>, where
    /// what it explains starts (in the surface's coordinates): its left at the line's, never out of the view on the
    /// left or on the right, the view being from <paramref name="viewLeft"/> across <paramref name="viewWidth"/> (zero
    /// while nothing has measured it); its bottom at the top of the line, or its top at the bottom of the line when
    /// the view has no room above. When something else holds one side of the line (<paramref name="aboveTaken"/>:
    /// the side above when true, the side below when false), the note takes the other, and stands nowhere, null,
    /// when that side has no room.
    /// </summary>
    internal static Point? Place(Rect line, float viewTop, float viewBottom, float viewLeft, float viewWidth,
        float width, float height, bool? aboveTaken)
    {
        var above = line.Y - height;
        var below = line.Y + line.Height;
        var roomAbove = above >= viewTop;
        var roomBelow = below + height <= viewBottom;
        float y;
        if (aboveTaken is null) y = roomAbove ? above : below;
        else if (aboveTaken == false)
        {
            if (!roomAbove) return null;
            y = above;
        }
        else
        {
            if (!roomBelow) return null;
            y = below;
        }
        var x = line.X;
        if (viewWidth > 0) x = MathF.Min(x, viewLeft + viewWidth - width);
        x = MathF.Max(x, viewLeft);
        return new Point(x, y);
    }

    private (VisualNode Card, float Width, float Height) Framed(VisualNode content, float height, bool announced)
    {
        var theme = _context.Theme;
        VisualNode card = new Box(new BoxStyle
        {
            Width = _width,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            BorderWidth = Border,
            BorderColor = theme.Border,
            Elevation = 2,
            Padding = EdgeInsets.Symmetric(PaddingAcross, PaddingDown),
            Clip = true,
        }, content);
        if (announced) card = new LiveRegion(card);
        return (card, _width, height);
    }

    /// <summary>
    /// How many lines <paramref name="paragraph"/> wraps to <paramref name="room"/> wide, broken between
    /// words as both targets break it: a word that does not fit on the line starts the next, and a word
    /// wider than the room takes as many lines as it is wide. Dividing the paragraph's width by the room
    /// counted the lines a wrap leaves half empty as full, and cut the last of them.
    /// </summary>
    private int WrappedLines(string paragraph, TypeStyle style, float room)
    {
        var lines = 1;
        var line = "";
        foreach (var word in paragraph.Split(' '))
        {
            var longer = line.Length == 0 ? word : line + " " + word;
            if (_context.MeasureText(longer, style) <= room)
            {
                line = longer;
                continue;
            }
            if (line.Length > 0) lines++;
            line = word;
            var wide = _context.MeasureText(word, style);
            if (wide > room) lines += (int)MathF.Ceiling(wide / room) - 1;
        }
        return lines;
    }
}
