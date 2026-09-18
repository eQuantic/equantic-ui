using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The two measurements that ask the HOST for metrics — <c>Text</c> and <c>TextEntry</c>,
/// both through <c>ctx.Measurer</c>. Every other node computes its box from numbers the tree already
/// carries, which is what makes this seam the one place a layout depends on the platform.</summary>
internal sealed partial class MeasureVisitor
{
    private LayoutNode MeasureText(Text text, LayoutConstraints constraints, LayoutContext ctx)
    {
        var result = ctx.Node(text);
        var maxW = constraints.MaxWidth;
        // The SAME resolver the realizers use. This built the merge by hand and therefore measured
        // without the theme's code face while PhotonRealizer rasterized with it — wrapping, widths
        // and a caret column computed against one face and drawn in another.
        var style = text.Resolve(ctx.Theme);
        var maxLines = LineCap(text, constraints);
        if (text.Spans is { Count: > 0 } spans)
            return MeasureRuns(result, text, spans, style, maxW, maxLines, ctx);
        var measurement = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, maxW, maxLines);
        result.Text = measurement;
        result.Bounds = new Rect(0, 0, measurement.Width, measurement.Height);
        return result;
    }

    /// <summary>
    /// How many lines this measurement may use — the text's own <see cref="Text.MaxLines"/>, or AT
    /// LEAST ONE when the parent is cutting it to fit (spec A2: text yields an ellipsis before a
    /// sibling is pushed out).
    /// <para>
    /// The cut used to live in the flex pass, which re-measured the text itself with this same
    /// <c>Max(1, …)</c>. Asking here is what lets the contract cut a WRAPPED text: the pass now
    /// re-measures the ITEM — <c>Pressable(Text(…))</c> and the bare text alike — and the cap
    /// arrives through the wrapper on the constraints, so both take one path instead of one path
    /// and one hand-built copy of it.
    /// </para>
    /// </summary>
    private static int LineCap(Text text, LayoutConstraints constraints) =>
        constraints.Truncating ? Math.Max(1, text.MaxLines) : text.MaxLines;

    /// <summary>
    /// A RICH paragraph: the runs are laid out as one flowing line of text, breaking between WORDS
    /// and never between runs. That distinction is the whole reason runs exist — a Row of Texts
    /// breaks at the run boundaries, so a sentence with three code spans wraps at the spans.
    /// <para>
    /// Each word is measured in its OWN run's style, because that is what decides its advance: bold
    /// is wider, mono is wider still, and a paragraph measured in the base style and drawn in five
    /// overflows its box by however much the emphasis added.
    /// </para>
    /// <para>
    /// The line BOX stays the paragraph's throughout, so a smaller inline code span does not reopen
    /// the leading — the same rule the web's `font-size`-without-`line-height` follows.
    /// </para>
    /// </summary>
    private LayoutNode MeasureRuns(LayoutNode result, Text text, IReadOnlyList<TextRun> spans,
        TypeStyle paragraph, float maxW, int maxLines, LayoutContext ctx)
    {
        var lineHeight = paragraph.ScaledLineHeight(ctx.TypeScale);
        var limit = float.IsPositiveInfinity(maxW) || maxW <= 0 ? float.PositiveInfinity : maxW;
        var fragments = new List<TextFragment>();
        var lines = new List<MeasuredLine>();

        float x = 0;
        var line = 0;
        float widest = 0;
        var cut = false;

        foreach (var run in spans)
        {
            if (cut) break;
            var runStyle = run.Resolve(paragraph, ctx.Theme);

            foreach (var word in Words(run.Content))
            {
                // A space that lands at a break is DROPPED rather than carried to the next line,
                // which is what keeps a wrapped paragraph's left edge straight.
                var width = ctx.Measurer.Measure(word, runStyle, ctx.TypeScale, float.PositiveInfinity, 1).Width;
                if (x > 0 && x + width > limit && word != " ")
                {
                    // The line is full, and a cap says there is no next one: the paragraph ends
                    // HERE, with the mark inside the measurement.
                    if (maxLines > 0 && line + 1 >= maxLines)
                    {
                        x = Ellipsize(fragments, runStyle, x, limit, line, lineHeight, ctx);
                        cut = true;
                        break;
                    }
                    lines.Add(new MeasuredLine(x, false));
                    if (x > widest) widest = x;
                    line++;
                    x = 0;
                }
                if (x == 0 && word == " ") continue;

                fragments.Add(new TextFragment(word, runStyle, x, line * lineHeight, width, line,
                    run.Color, run.Destination is { Length: > 0 } ? run.Destination : null));
                x += width;
            }
        }

        lines.Add(new MeasuredLine(x, cut));
        if (x > widest) widest = x;

        result.TextRuns = fragments;
        result.Text = new TextMeasurement(widest, lines.Count * lineHeight, lineHeight, lines);
        result.Bounds = new Rect(0, 0, widest, lines.Count * lineHeight);
        return result;
    }

    /// <summary>
    /// Ends a cut line with the mark, INSIDE the width the measurement reports — the promise
    /// <see cref="ITextMeasurer"/> makes and the plain path already keeps.
    ///
    /// <para>
    /// Trailing words are dropped until the mark fits, which the plain path never has to do: there
    /// a cut line is a NUMBER and the rasterizer re-wraps from the string, while here every word
    /// carries the rectangle it will be drawn in. Clamping the reported width without dropping them
    /// would leave glyphs positioned past the box the width promised.
    /// </para>
    ///
    /// <para>
    /// Never below one word. A line that kept nothing would report a mark standing where a word had
    /// been, and an ellipsis alone tells a reader less than a cut word does. When that one word is
    /// itself wider than the room — nothing left to drop — the reported width CLAMPS to the limit,
    /// which is what the plain path has always done (<c>Min(lineWidth + ellipsis, maxWidth)</c>).
    /// An unbreakable word overflows its box on both paths and the realizer clips it; what must not
    /// differ between them is the NUMBER handed back to layout, because a node reporting more room
    /// than it was given makes its parent grow.
    /// </para>
    ///
    /// <para>
    /// THE MARK BELONGS TO THE TEXT IT TERMINATES, not to the word that failed to fit. Those are
    /// different runs as often as not — the word that overflowed is usually the first of the NEXT
    /// run, whose face the reader never sees on this line — so it takes the style, the ink and the
    /// link of the last VISIBLE fragment left on the line. The link matters most: an ellipsis is
    /// the tail of the sentence it cut, and one that carried no destination made the end of a
    /// truncated link unpressable on a target that hit-tests per fragment.
    /// </para>
    /// </summary>
    private float Ellipsize(List<TextFragment> fragments, TypeStyle fallback, float x, float limit,
        int line, float lineHeight, LayoutContext ctx)
    {
        const string mark = "\u2026";

        // The last fragment on the line that a reader can SEE. A trailing space is skipped because
        // it carries the next run's face and none of its ink — which is exactly the mismatch this
        // is here to avoid, one fragment further along.
        TextFragment? Tail()
        {
            for (var i = fragments.Count - 1; i >= 0 && fragments[i].Line == line; i--)
                if (!string.IsNullOrWhiteSpace(fragments[i].Content)) return fragments[i];
            return null;
        }

        var tail = Tail();
        var markWidth = MarkWidth();

        while (x + markWidth > limit && fragments.Count > 1
               && fragments[^1].Line == line && fragments[^2].Line == line)
        {
            x = fragments[^1].X;
            fragments.RemoveAt(fragments.Count - 1);
            // Dropping a word can change which run ends the line, and a narrower face needs less
            // room for the mark — so both are asked again rather than once at the top.
            tail = Tail();
            markWidth = MarkWidth();
        }

        fragments.Add(new TextFragment(mark, tail?.Style ?? fallback, x, line * lineHeight,
            markWidth, line, tail?.Color, tail?.Destination));
        // `limit` is already positive infinity when the room is unbounded, and Min against it
        // is the identity — so the unbounded case needs no arm of its own.
        return MathF.Min(x + markWidth, limit);

        float MarkWidth() => ctx.Measurer
            .Measure(mark, tail?.Style ?? fallback, ctx.TypeScale, float.PositiveInfinity, 1).Width;
    }

    /// <summary>
    /// A run split into the pieces a line can break between: words, with each separating space as a
    /// piece of its own so the break can drop it. Deliberately not a tokenizer — the subset that
    /// matters is "space breaks, everything else does not", the same rule the measurers use.
    /// </summary>
    private List<string> Words(string content)
    {
        var pieces = new List<string>();
        var start = 0;
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != ' ') continue;
            if (i > start) pieces.Add(content[start..i]);
            pieces.Add(" ");
            start = i + 1;
        }
        if (start < content.Length) pieces.Add(content[start..]);
        return pieces;
    }

    /// <summary>A text entry is <see cref="TextEntry.Lines"/> lines of its role (1 by default),
    /// filling the available width (the field's editable area) — height from the type scale so forms
    /// lay out identically before and after the real caret/IME land (spec B9's fixed contract). A
    /// multi-line field is exactly that many lines TALL whatever it currently holds: its box must not
    /// grow and shrink as the user types.</summary>
    private LayoutNode MeasureTextEntry(TextEntry entry, float maxW, LayoutContext ctx)
    {
        var result = ctx.Node(entry);
        var style = ctx.Theme.Type(entry.Role);
        var shown = entry.Value.Length > 0 ? entry.Value : entry.Placeholder ?? string.Empty;
        var lines = Math.Max(1, entry.Lines);
        var measurement = ctx.Measurer.Measure(shown, style, ctx.TypeScale, maxW, maxLines: lines);
        result.Text = measurement;
        var width = float.IsFinite(maxW) ? maxW : measurement.Width;
        var height = lines == 1 ? measurement.Height : measurement.LineHeight * lines;
        result.Bounds = new Rect(0, 0, width, height);
        return result;
    }
}
