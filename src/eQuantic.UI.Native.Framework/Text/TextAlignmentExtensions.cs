using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Where a line sits inside the block that holds it — the whole of what <see cref="TextAlignment"/>
/// means, as ONE function rather than one per platform. Three rasterizers place glyph runs and the
/// headless placeholder draws bars: four answers to one question is four ways to disagree, and a
/// paragraph that centres on the Mac while it hugs the left on Android is a defect no suite in this
/// repo can see.
/// </summary>
public static class TextAlignmentExtensions
{
    /// <summary>
    /// How far right a line of <paramref name="lineWidth"/> starts inside a block of
    /// <paramref name="blockWidth"/>. Never negative: a line WIDER than its block overflows to the
    /// right, the way every text engine overflows one, instead of starting off the left edge.
    /// </summary>
    public static float Offset(this TextAlignment align, float blockWidth, float lineWidth) => align switch
    {
        TextAlignment.Center => MathF.Max(0, (blockWidth - lineWidth) / 2),
        TextAlignment.End => MathF.Max(0, blockWidth - lineWidth),
        _ => 0f,
    };

    /// <summary>
    /// The block a line aligns inside: the CONTENT width normally, the BOX width when the box is
    /// wider and the text was asked to align in it.
    /// <para>
    /// A hugging paragraph still aligns, and this is the case the bug was reported from. Its block
    /// is its own longest line, so the short lines centre against that one — which is exactly what
    /// centred text looks like in a box cut tight to it. Widening is for the STRETCHED box, and it
    /// is why a raster grows only where alignment was asked for: an unbounded width (a field being
    /// typed into, a run measured on its own) has no box to align inside and keeps the tight raster
    /// it always had.
    /// </para>
    /// </summary>
    public static float BlockWidth(this TextAlignment align, float contentWidth, float boxWidth) =>
        align == TextAlignment.Start || !float.IsFinite(boxWidth) || boxWidth <= contentWidth
            ? contentWidth
            : boxWidth;
}
