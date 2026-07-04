using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>One laid-out line of a measured paragraph.</summary>
public readonly record struct MeasuredLine(float Width, bool Ellipsized);

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

    private static void WrapParagraph(string paragraph, float size, float tracking, float maxWidth, int maxLines,
        List<MeasuredLine> lines)
    {
        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var spaceWidth = Advance(' ', size) + tracking;
        var lineWidth = 0f;
        var lineHasContent = false;

        void CommitLine(bool ellipsized = false)
        {
            lines.Add(new MeasuredLine(lineWidth, ellipsized));
            lineWidth = 0;
            lineHasContent = false;
        }

        foreach (var word in words)
        {
            var wordWidth = WordWidth(word, size, tracking);
            var candidate = lineHasContent ? lineWidth + spaceWidth + wordWidth : wordWidth;

            if (candidate <= maxWidth || !lineHasContent)
            {
                // Fits (or is the first word — a single overlong word occupies the line, clipped by wrap).
                lineWidth = MathF.Min(candidate, maxWidth is float.PositiveInfinity ? candidate : maxWidth);
                lineHasContent = true;
                continue;
            }

            // Wrap. If the NEXT line would exceed maxLines, ellipsize this one instead (spec A8).
            if (maxLines > 0 && lines.Count + 1 >= maxLines)
            {
                lineWidth = MathF.Min(lineWidth + EllipsisAdvance * size,
                    maxWidth is float.PositiveInfinity ? float.MaxValue : maxWidth);
                CommitLine(ellipsized: true);
                return;
            }
            CommitLine();
            lineWidth = MathF.Min(wordWidth, maxWidth is float.PositiveInfinity ? wordWidth : maxWidth);
            lineHasContent = true;
        }

        if (lineHasContent || words.Length == 0) CommitLine();
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
