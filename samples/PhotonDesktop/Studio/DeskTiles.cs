using eQuantic.UI.Primitives;

namespace eQuantic.Studio;

/// <summary>
/// The desk's artwork, inline: two vector drawings and two image tiles.
/// <para>
/// The drawings are the SVG text the web dashboard keeps under <c>Assets</c>, read by the same
/// <see cref="SvgDocument"/> the build's vector catalog reads it with. This SDK writes no <c>Vectors</c>
/// catalog for a Photon app (only the web SDK does), so a Photon sample parses the text itself.
/// </para>
/// The image tiles, inline: a PNG 360 px square for a 120 dp tile, a teal ink for light and an
/// amber one for dark, on a transparent ground. Photon's image loader decodes a source that is a file's
/// path or a data URI, and an app of this sample carries no file beside its binary, so the bytes ride in
/// the source as the data URI an <c>Image</c> takes. They are the same two files the web dashboard
/// serves from <c>wwwroot/desk</c>.
/// </summary>
public static class DeskTiles
{
    /// <summary>The desk tile: circles and rules in one fixed ink.</summary>
    public static readonly VectorDrawing Tile = SvgDocument.Parse("""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 240 160">
  <circle cx="60" cy="48" r="22" fill="none" stroke="#8A6F4E" stroke-width="2"/>
  <circle cx="60" cy="48" r="6" fill="#8A6F4E"/>
  <circle cx="180" cy="112" r="30" fill="none" stroke="#8A6F4E" stroke-width="2"/>
  <circle cx="180" cy="112" r="10" fill="#8A6F4E"/>
  <line x1="110" y1="20" x2="160" y2="20" stroke="#8A6F4E" stroke-width="2"/>
  <line x1="110" y1="32" x2="140" y2="32" stroke="#8A6F4E" stroke-width="2"/>
  <line x1="20" y1="130" x2="110" y2="130" stroke="#8A6F4E" stroke-width="2"/>
  <line x1="20" y1="142" x2="80" y2="142" stroke="#8A6F4E" stroke-width="2"/>
  <line x1="170" y1="40" x2="226" y2="40" stroke="#8A6F4E" stroke-width="2"/>
</svg>
""");

    /// <summary>A tile whose shapes are <c>currentColor</c>, tinted by whatever holds them.</summary>
    public static readonly VectorDrawing Ink = SvgDocument.Parse("""
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120">
  <circle cx="60" cy="60" r="38" fill="none" stroke="currentColor" stroke-width="3"/>
  <circle cx="60" cy="60" r="8" fill="currentColor"/>
  <line x1="10" y1="60" x2="30" y2="60" stroke="currentColor" stroke-width="3"/>
  <line x1="90" y1="60" x2="110" y2="60" stroke="currentColor" stroke-width="3"/>
  <line x1="60" y1="10" x2="60" y2="30" stroke="currentColor" stroke-width="3"/>
  <line x1="60" y1="90" x2="60" y2="110" stroke="currentColor" stroke-width="3"/>
</svg>
""");

    /// <summary>The light page's tile.</summary>
    public const string PaperLight = "data:image/png;base64,"
        + "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"
        + "uN5j9iPgbDjPgMtDGlNm07cE+tNI4/U1vp3D7kHA2XiegZePNKbOpq2KGmksE/PYUzxwTiDPAMxPGvuJN7QlUSON+WKecg4ezonkGYj5hnlKr/"
        + "J5g8fsUSONP3vMt4nX7gTnPL0ByLzDPHW3v3nDb+CselzuNOM6TT2dg5wTyzMwfYR5M7E/3exnlnAWbWPMvS4bOOfvDYD6CvNcUUvNqj1KY+5s"
        + "ea6YkXPC3gCqzzAvEfUtkKw9SWMzs7e8VMzIOXFvANZ3mJeIeomsvXwJLpHyUjEj5wy8AVzCPOcJtS5ZVyOOeuXMeV8zuC1kGeI0DXLOwBsAJs"
        + "xtUZcBBPOc/Z1aws6N874ezzUAr+d59II84w0ETZjHLNGrAOJ51vu9fr/Xz+c037C3acyQrwG5VIF5kOeMvAFkwjx2Vh1SSs1ZdlX/+3sj4n6K"
        + "uBSQcfv4YkGe8QaCJswpyPrVzLJqCXy/QOSb1r9Rtj5DejxSUibPGXsDyAQuBVmnWtJSJs+ZewPIlLUTDCnX2JMs5BlvIGjCHK1+uNfbvX7NXN"
        + "hNIRfkGW8gaMKcIueicSStSljIVePoYEGe8QaCJsw5c943WiOVMRFXjb+PPJPnTwiaMMP5f2fcbYFXC46+XV/8O83P4LcaKXOcgUzBGc6UUc5A"
        + "puAMZ8ooZyBTcIYzZZQzkCk4w5kyyhnIFJzhDGejnIFMwRnOcDbKGcgUnOEMZ6N/I5ApOMMZzgiaMMOZgjOcvQiaMMMZzlTWnIFMwRnOcEbQhB"
        + "nOFJzhnLOgCTOc4Uy54QxkCs5whjOCJsxwpuAM59wETZjhDGfKJWcgU3CGM5wRNGGGM5zhDOccBE2Y4Qxnyj1nIBNmOMMZzgiaMMMZzhScUxU0"
        + "YYYznCk4GxQ0YYYznCk4GxB0ca9dXYUDyO3x5h5mK+Mlz+Q5+fFqQ30M8qNVVcZhfjXeXcZhtjJe8kyesxiv9jfvR0cVmc40LIxXc6bB9WW85D"
        + "lRQe96BrxzMtvQHq/mcpvry3jJM4Im0EY3ULi+jJc80+JgSWh0d5vry3jJc8KC/lJvoHjfRNplGGaPm4TkmTxnI+gmZO/HsHI/F+rh+pJn8pyN"
        + "oDm0z6F9OFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNi"
        + "hoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNiho"
        + "IBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNihoIBNmOFNwNipoIBNmOFNwNsj5X7SjMGnhkrLbAA"
        + "AAAElFTkSuQmCC";

    /// <summary>The dark page's tile.</summary>
    public const string PaperDark = "data:image/png;base64,"
        + "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"
        + "Pk243mP2I+BsOM+Ay0MaU2bT1wT600jj9TW+3sLuQcDZeJ6Bl480ps6mrYoaaSwT89hTPHBOIM8AzE8a+4k3tCVRI435Yp5yDh7OieQZiPmGeU"
        + "qv8nmDx+xRI40/e8zXidfuBOc8vQHIvMM8dbe/ecNv4Kx6XO404zpNPZ2DnBPLMzB9hHkzsT/d7GeWcBZtY8y9Lhs45+8NgPoK81xRS82qPUpj"
        + "7mx5rpiRc8LeAKrPMC8R9TWQrD1JYzOzt7xUzMg5cW8A1neYl4h6iay9fAkukfJSMSPnDLwBXMI85wm1LllXI4565cx5XzO4LmQZ4jQNcs7AGw"
        + "AmzG1RlwEE85z9nVrCzo3zvh7PJQCv53n0gjzjDQRNmMcs0asA4nnW+71+v9fPtzTfsLdpzJAvAblUgXmQ54y8AWTCPHZWHVJKzVl2Vf/7eyPi"
        + "foq4FJBx+/hiQZ7xBoImzCnI+tXMsmoJfL9A5JvWv1G2PkN6PFJSJs8ZewPIBC4FWada0lImz5l7A8iUtRMMKdfYkyzkGW8gaMIcrX6419u9fs"
        + "1c2E0hF+QZbyBowpwi56JxJK1KWMhV4+hgQZ7xBoImzDlz3jdaI5UxEVeNv488k+dPCJoww/l/Z9xtgVcLjr5dXvw7zc/gtxopc5yBTMEZzpRR"
        + "zkCm4AxnyihnIFNwhjNllDOQKTjDmTLKGcgUnOEMZ6OcgUzBGc5wNsoZyBSc4Qxno38jkCk4wxnOCJoww5mCM5y9CJowwxnOVNacgUzBGc5wRt"
        + "CEGc4UnOGcs6AJM5zhTLnhDGQKznCGM4ImzHCm4Azn3ARNmOEMZ8olZyBTcIYznBE0YYYznOEM5xwETZjhDGfKPWcgE2Y4wxnOCJowwxnOFJxT"
        + "FTRhhjOcKTgbFDRhhjOcKTgbEHRxr11dhQPI7fHmHmYr4yXP5Dn58WpDfQzyo1VVxmF+Nd5dxmG2Ml7yTJ6zGK/2N+9HRxWZzjQsjFdzpsH1Zb"
        + "zkOVFB73oGvHMy29Aer+Zym+vLeMkzgibQRjdQuL6MlzzT4mBJaHR3m+vLeMlzwoL+Um+geN9E2mUYZo+bhOSZPGcj6CZk78ewcj8X6uH6kmfy"
        + "nI2gObTPoX04U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3"
        + "A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2"
        + "KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KGggE2Y4U3A2KmggE2Y4U3A2yPlf7HoEaMxGTb"
        + "4AAAAASUVORK5CYII=";
}
