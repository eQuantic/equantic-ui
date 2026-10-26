namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// A weight in CoreText's own scale, −1 to 1, the one its weight trait is asked in (#1082). Its own type,
/// apart from <see cref="CoreTextService"/>, because that one loads CoreText as it starts, and a scale is
/// arithmetic any machine can check.
/// </summary>
internal static class CoreTextWeights
{
    /// <summary>
    /// The anchors: the values of Apple's nine named weights (<c>NSFontWeight</c>), keyed by the weight
    /// class each one is. Apple calls 100 ultraLight, 200 thin and 800 heavy, where OpenType says Thin,
    /// ExtraLight and ExtraBold.
    /// </summary>
    private static readonly (int Class, double Scale)[] Anchors =
    [
        (100, -0.8), (200, -0.6), (300, -0.4), (400, 0.0), (500, 0.23), (600, 0.3), (700, 0.4), (800, 0.56), (900, 0.62),
    ];

    /// <summary>
    /// <paramref name="weightClass"/> in CoreText's scale, read off its NUMBER rather than its name: between
    /// two anchors it is interpolated, and beyond them it is the nearest, so no weight falls to a default.
    /// </summary>
    public static double Scale(int weightClass)
    {
        if (weightClass <= Anchors[0].Class) return Anchors[0].Scale;
        for (var i = 1; i < Anchors.Length; i++)
        {
            var (high, highScale) = Anchors[i];
            if (weightClass > high) continue;
            var (low, lowScale) = Anchors[i - 1];
            return lowScale + (highScale - lowScale) * (weightClass - low) / (high - low);
        }
        return Anchors[^1].Scale;
    }
}
