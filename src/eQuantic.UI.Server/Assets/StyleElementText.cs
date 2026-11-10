namespace eQuantic.UI.Server.Assets;

/// <summary>
/// The text a <c>&lt;style&gt;</c> element is written with. An HTML parser ends the element at the first
/// <c>&lt;/style</c>, wherever it stands, so a value that held one (from the <c>HtmlStyle</c> escape
/// hatch, a face's name, an app's own CSS) ended the element early (#1295). Every <c>&lt;/</c> is written
/// <c>\3c /</c>, which CSS
/// reads as the same two characters inside a string or a <c>url()</c>, and which never appears anywhere else
/// in a stylesheet: a selector or a media query has no <c>&lt;/</c>.
/// <para>
/// The one writer of that text: <see cref="InlineStyleAsset"/>, the theme's sheet and the app's own styles
/// in the shell all go through it.
/// </para>
/// </summary>
internal static class StyleElementText
{
    /// <summary><paramref name="css"/> as a style element may hold it.</summary>
    public static string Of(string css) => css.Replace("</", @"\3c /", StringComparison.Ordinal);
}
