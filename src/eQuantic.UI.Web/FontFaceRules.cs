using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The <c>@font-face</c> rules for the faces an app ships (#1055): one per file, with its family, its
/// URL, its weights and its style. The host knows each file's URL, because it serves the files; this
/// knows how a browser is told about them.
/// <para>
/// <c>font-display: swap</c> on every rule, so text is drawn at once in the fallback and set in the
/// face when it arrives, instead of being invisible while it loads. A rule costs nothing until text
/// uses its family: a browser fetches a face only when it sets text in it.
/// </para>
/// </summary>
public static class FontFaceRules
{
    /// <summary>
    /// The rules for <paramref name="faces"/>, each file at the URL given beside it. A family no page
    /// can be set in (<see cref="FaceName.IsWellFormed"/>) is left out: the build refuses one in an app's
    /// catalog (EQ4005), and the theme's stylesheet reports one the theme names.
    /// </summary>
    public static string Write(IEnumerable<(FontFamily Family, FontFile File, string Url)> faces)
    {
        var css = new StringBuilder();
        foreach (var (family, file, url) in faces)
        {
            if (!FaceName.IsWellFormed(family.Name)) continue;
            css.Append("@font-face { font-family: \"").Append(family.Name)
                .Append("\"; src: url(\"").Append(url).Append("\") format(\"").Append(Format(file))
                .Append("\"); font-weight: ").Append(file.MinWeight == file.MaxWeight
                    ? $"{file.MinWeight}"
                    : $"{file.MinWeight} {file.MaxWeight}")
                .Append("; font-style: ").Append(file.Italic ? "italic" : "normal")
                .AppendLine("; font-display: swap; }");
        }
        return css.ToString();
    }

    /// <summary>The format a rule names for <paramref name="file"/>: <c>opentype</c> for CFF, <c>truetype</c> otherwise.</summary>
    public static string Format(FontFile file) => IsOpenTypeCff(file) ? "opentype" : "truetype";

    /// <summary>The media type <paramref name="file"/> is served as.</summary>
    public static string ContentType(FontFile file) => IsOpenTypeCff(file) ? "font/otf" : "font/ttf";

    private static bool IsOpenTypeCff(FontFile file) => file.Resource.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);
}
