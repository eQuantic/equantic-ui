namespace eQuantic.UI.Native.Engine;

/// <summary>
/// How a gradient mixes its two stops (#1956). Both mix in sRGB space; they differ where a stop is
/// translucent and its neighbour is another colour. Each follows the target it answers to: a box's or
/// a text's gradient is a CSS gradient on the web, and an artwork's runs are SVG paint servers.
/// Measured in Chromium 156 over white, red into transparent black reads (255, 128, 128) at its middle
/// as a CSS gradient and (191, 128, 128) as an SVG one.
/// </summary>
public enum GradientInterpolation : byte
{
    /// <summary>Each channel mixed on its own, alpha included: what an SVG paint server does.</summary>
    Straight = 0,

    /// <summary>
    /// The colours mixed weighted by their alpha, then divided by the mixed alpha: what a CSS gradient
    /// does (CSS Images, "premultiplied"). A colour fading into transparent keeps its own colour all
    /// the way, whatever colour the transparent stop names.
    /// </summary>
    Premultiplied = 1,
}
