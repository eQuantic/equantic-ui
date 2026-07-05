using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material.ColorScience;

/// <summary>
/// A Material 3 tonal palette: one hue+chroma swept across TONE (0..100). <c>Tone(40)</c> and
/// <c>Tone(80)</c> are the light/dark "primary" tones, <c>Tone(90)/Tone(30)</c> the containers, etc.
/// Built from a seed via CAM16/HCT (see <see cref="Hct"/>).
/// </summary>
internal readonly struct TonalPalette
{
    private readonly double _hue;
    private readonly double _chroma;

    private TonalPalette(double hue, double chroma)
    {
        _hue = hue;
        _chroma = chroma;
    }

    public static TonalPalette FromHueAndChroma(double hue, double chroma) => new(hue, chroma);

    /// <summary>The palette a seed color anchors (its own hue+chroma).</summary>
    public static TonalPalette FromColor(Color seed)
    {
        var hct = Hct.FromColor(seed);
        return new TonalPalette(hct.Hue, hct.Chroma);
    }

    public Color Tone(double tone) => Hct.ToColor(_hue, _chroma, tone);
}
