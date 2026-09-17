using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>The two measurements that ask the HOST for metrics — <c>Text</c> and <c>TextEntry</c>,
/// both through <c>ctx.Measurer</c>. Every other node computes its box from numbers the tree already
/// carries, which is what makes this seam the one place a layout depends on the platform.</summary>
internal sealed partial class MeasureVisitor
{
    private LayoutNode MeasureText(Text text, float maxW, LayoutContext ctx)
    {
        var result = ctx.Node(text);
        // The SAME resolver the realizers use. This built the merge by hand and therefore measured
        // without the theme's code face while PhotonRealizer rasterized with it — wrapping, widths
        // and a caret column computed against one face and drawn in another.
        var style = text.Resolve(ctx.Theme);
        if (text.Spans is { Count: > 0 } spans) return MeasureRuns(result, text, spans, style, maxW, ctx);
        var measurement = ctx.Measurer.Measure(text.PlainContent, style, ctx.TypeScale, maxW, text.MaxLines);
        result.Text = measurement;
        result.Bounds = new Rect(0, 0, measurement.Width, measurement.Height);
        return result;
    }

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
        TypeStyle paragraph, float maxW, LayoutContext ctx)
    {
        var lineHeight = paragraph.ScaledLineHeight(ctx.TypeScale);
        var limit = float.IsPositiveInfinity(maxW) || maxW <= 0 ? float.PositiveInfinity : maxW;
        var fragments = new List<TextFragment>();
        var lines = new List<MeasuredLine>();

        float x = 0;
        var line = 0;
        float widest = 0;

        foreach (var run in spans)
        {
            var runStyle = run.Resolve(paragraph, ctx.Theme);

            foreach (var word in Words(run.Content))
            {
                // A space that lands at a break is DROPPED rather than carried to the next line,
                // which is what keeps a wrapped paragraph's left edge straight.
                var width = ctx.Measurer.Measure(word, runStyle, ctx.TypeScale, float.PositiveInfinity, 1).Width;
                if (x > 0 && x + width > limit && word != " ")
                {
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

        lines.Add(new MeasuredLine(x, false));
        if (x > widest) widest = x;

        result.TextRuns = fragments;
        result.Text = new TextMeasurement(widest, lines.Count * lineHeight, lineHeight, lines);
        result.Bounds = new Rect(0, 0, widest, lines.Count * lineHeight);
        return result;
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
