using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>One laid-out line of a measured paragraph.</summary>
public readonly record struct MeasuredLine(float Width, bool Ellipsized);

/// <summary>
/// One piece of a RICH paragraph as the layout placed it: the text, the style it is drawn in, and
/// where it sits inside the paragraph's box. A run that wraps produces several of these — a
/// fragment is a run ON A LINE, which is the only unit that has a rectangle.
/// </summary>
/// <param name="Content">The run's text as it is drawn on this line.</param>
/// <param name="Style">The face it is drawn in, which is what the measurement was made against.</param>
/// <param name="X">Its left edge inside the paragraph's box, dp.</param>
/// <param name="Y">Its top edge inside the paragraph's box, dp.</param>
/// <param name="Width">How wide the piece measured, dp.</param>
/// <param name="Color">Its ink, or null to take the block's own.</param>
/// <param name="Line">
/// Which line of the paragraph this piece landed on. <see cref="Y"/> already implies it, but only
/// by dividing back through the line height — and the caller that needs it is aligning the line,
/// which means reading that line's width out of the measurement by index.
/// </param>
/// <param name="Destination">
/// Where this piece navigates to, when it belongs to a linked run. The rectangle is why it is here:
/// a link inside a sentence can only be pressed if something knows which pixels are the link, and
/// on a target that draws its own glyphs, nothing else does.
/// </param>
public readonly record struct TextFragment(
    string Content,
    TypeStyle Style,
    float X,
    float Y,
    float Width,
    int Line,
    Primitives.ColorToken? Color = null,
    string? Destination = null);

/// <summary>A measured paragraph: overall block size plus per-line widths (for placeholder rendering
/// and, later, glyph placement).</summary>
public sealed class TextMeasurement
{
    public TextMeasurement(float width, float height, float lineHeight, IReadOnlyList<MeasuredLine> lines)
    {
        Width = width;
        Height = height;
        LineHeight = lineHeight;
        Lines = lines;
    }

    public float Width { get; }
    public float Height { get; }
    public float LineHeight { get; }
    public IReadOnlyList<MeasuredLine> Lines { get; }
}

/// <summary>
/// The text-measurement seam of the layout engine. The REAL implementation is the W4 text stack
/// (HarfBuzz shaping + FreeType metrics, cached by (text, style, width)); until it lands,
/// <see cref="ApproximateTextMeasurer"/> provides deterministic stand-in metrics so layout, tests and
/// goldens are exercisable today. Layout only ever talks to this interface — swapping the real shaper
/// in changes measurements, never layout code.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>
    /// Measures <paramref name="content"/> in <paramref name="style"/> (already Dynamic-Type scaled by
    /// the caller via <paramref name="typeScale"/>), wrapped at <paramref name="maxWidth"/>
    /// (<see cref="float.PositiveInfinity"/> = unconstrained) and truncated to
    /// <paramref name="maxLines"/> (0 = unlimited) with a trailing ellipsis.
    ///
    /// <para>
    /// THE MARK IS INSIDE THE MEASUREMENT. An implementation truncates in its own LAYOUT, so the
    /// width reported for a cut line already includes the ellipsis and the glyphs a rasterizer draws
    /// are that same line. Nothing downstream adds a mark: a realizer that drew one on top would be
    /// drawing outside the width this method promised.
    /// </para>
    ///
    /// <para>
    /// It was not always so, and the contract is the reason it is now. This sentence promised an
    /// ellipsis from the day it was written while a cut line ended four different ways — the web drew
    /// it with CSS, Android put the character in the string, CoreText and DirectWrite cut and drew
    /// nothing, with the exemption in one platform class's doc comment where nobody reading this
    /// interface would find it. Nothing asked the implementations whether they met the promise.
    /// `TruncationContractTests` asks them now.
    /// </para>
    /// </summary>
    TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines);
}

/// <summary>
/// Deterministic stand-in metrics (W4 pending): average per-character advances for a humanist
/// sans (Hanken Grotesk-like), greedy word wrap, shaping-time-style ellipsis. Not typographically
/// exact — DETERMINISTIC, so layout geometry tests and goldens are stable until HarfBuzz replaces it
/// (at which point goldens regenerate once, by design).
/// </summary>
public sealed class ApproximateTextMeasurer : ITextMeasurer
{
    public static readonly ApproximateTextMeasurer Instance = new();

    private const float DefaultAdvance = 0.52f; // × font size
    private const float NarrowAdvance = 0.28f;
    private const float WideAdvance = 0.82f;
    private const float EllipsisAdvance = 0.60f;

    public TextMeasurement Measure(string content, TypeStyle style, float typeScale, float maxWidth, int maxLines)
    {
        var size = style.ScaledSize(typeScale);
        var lineHeight = style.ScaledLineHeight(typeScale);
        var tracking = style.Tracking;

        var lines = new List<MeasuredLine>();
        var maxLineWidth = 0f;

        foreach (var paragraph in content.Split('\n'))
        {
            WrapParagraph(paragraph, size, tracking, maxWidth, maxLines, lines);
            if (maxLines > 0 && lines.Count >= maxLines) break;
        }
        if (lines.Count == 0) lines.Add(new MeasuredLine(0, false));

        foreach (var line in lines) maxLineWidth = MathF.Max(maxLineWidth, line.Width);
        return new TextMeasurement(maxLineWidth, lines.Count * lineHeight, lineHeight, lines);
    }

    /// <summary>
    /// Lays one paragraph into lines. Every space takes its advance, as a shell's measurer charges it:
    /// the ones between two words on a line, the ones the paragraph starts or ends with, and a run that
    /// is nothing but a space, which is how a rich paragraph asks for the gap between its words. A
    /// break drops the spaces it falls on. Split on spaces and emptied of them, a lone space measured
    /// zero, and every gap of a rich paragraph cost nothing (#285).
    /// </summary>
    private static void WrapParagraph(string paragraph, float size, float tracking, float maxWidth, int maxLines,
        List<MeasuredLine> lines)
    {
        var spaceWidth = Advance(' ', size) + tracking;
        var cap = maxWidth is float.PositiveInfinity ? float.MaxValue : maxWidth;
        var lineWidth = 0f;
        var lineHasContent = false;
        // The spaces since the last word, which the next word on this line or the paragraph's end
        // takes, and a break drops.
        var spaces = 0f;

        void CommitLine(bool ellipsized = false)
        {
            lines.Add(new MeasuredLine(lineWidth, ellipsized));
            lineWidth = 0;
            lineHasContent = false;
        }

        var start = 0;
        for (var i = 0; i <= paragraph.Length; i++)
        {
            if (i < paragraph.Length && paragraph[i] != ' ') continue;
            if (i > start)
            {
                var wordWidth = WordWidth(paragraph.Substring(start, i - start), size, tracking);
                var candidate = lineWidth + spaces + wordWidth;
                if (candidate <= maxWidth || !lineHasContent)
                {
                    // Fits (or is the line's first word — a single overlong word occupies the line,
                    // clipped by wrap).
                    lineWidth = MathF.Min(candidate, cap);
                    lineHasContent = true;
                }
                else
                {
                    // Wrap. If the NEXT line would exceed maxLines, ellipsize this one instead (spec A8).
                    if (maxLines > 0 && lines.Count + 1 >= maxLines)
                    {
                        lineWidth = MathF.Min(lineWidth + EllipsisAdvance * size, cap);
                        CommitLine(ellipsized: true);
                        return;
                    }
                    CommitLine();
                    lineWidth = MathF.Min(wordWidth, cap);
                    lineHasContent = true;
                }
                spaces = 0;
            }
            if (i < paragraph.Length) spaces += spaceWidth;
            start = i + 1;
        }

        if (spaces > 0)
        {
            lineWidth = MathF.Min(lineWidth + spaces, cap);
            lineHasContent = true;
        }
        if (lineHasContent || paragraph.Length == 0) CommitLine();
    }

    private static float WordWidth(string word, float size, float tracking)
    {
        var width = 0f;
        foreach (var c in word) width += Advance(c, size) + tracking;
        return width;
    }

    private static float Advance(char c, float size) => size * (c switch
    {
        ' ' => 0.30f,
        'i' or 'j' or 'l' or 'I' or '.' or ',' or ':' or ';' or '\'' or '!' or '|' or '(' or ')' => NarrowAdvance,
        'm' or 'w' or 'M' or 'W' or '@' => WideAdvance,
        _ => DefaultAdvance,
    });
}
