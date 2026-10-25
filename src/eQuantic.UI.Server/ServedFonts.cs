using System.Reflection;
using System.Security.Cryptography;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server;

/// <summary>
/// The faces the web serves (#1055): every file of every family the theme names, and of every family
/// in the catalogs of the assemblies the app scans. Each file is read once from the assembly it is
/// embedded in and served at a URL addressed by its content, so the route serves only what was named
/// and has no path to walk.
/// <list type="bullet">
/// <item>The theme's families are its text face, its code face and every role's face. Naming a family
/// is what ships it, so nothing lists them twice.</item>
/// <item>A catalog's families (<see cref="FontCatalogAttribute"/>) are declared too, so a family a page
/// names at a call site loads as the theme's do. A rule costs nothing until text uses its family.</item>
/// </list>
/// </summary>
internal sealed class ServedFonts
{
    private readonly Dictionary<string, ServedFont> _byName;

    private ServedFonts(IReadOnlyList<ServedFont> fonts, FontFamily? text)
    {
        Fonts = fonts;
        _byName = fonts.GroupBy(font => font.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Rules = FontFaceRules.Write(fonts.Select(font => (font.Family, font.File, font.Url)));
        // The text face's upright files set nearly every glyph of every page, so their fetch starts
        // from the head. Every other face loads when text first uses it: a preload a page does not use
        // costs its bytes and delays the ones it does.
        Preloads = string.Join("\n        ", fonts
            .Where(font => text is not null && font.Family.Equals(text) && !font.File.Italic)
            .Select(font => $"<link rel=\"preload\" href=\"{font.Url}\" as=\"font\" type=\"{FontFaceRules.ContentType(font.File)}\" crossorigin>"));
    }

    /// <summary>Every file served, in the order the rules declare them.</summary>
    public IReadOnlyList<ServedFont> Fonts { get; }

    /// <summary>The <c>@font-face</c> rules, written into the theme's stylesheet.</summary>
    public string Rules { get; }

    /// <summary>The head's preloads for the text face's upright files.</summary>
    public string Preloads { get; }

    /// <summary>The file served under <paramref name="name"/>, or null for a name nothing was served under.</summary>
    public ServedFont? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>
    /// The faces <paramref name="theme"/> and the catalogs of <paramref name="assemblies"/> ship. A file
    /// whose assembly or resource is not there is left out, and said once to <paramref name="logger"/>.
    /// </summary>
    public static ServedFonts Of(IAppTheme theme, IEnumerable<Assembly> assemblies, ILogger? logger)
    {
        var families = ThemeFamilies(theme).Concat(CatalogFamilies(assemblies))
            .Where(family => family.Files.Count > 0)
            .Distinct()
            .ToList();
        var fonts = new List<ServedFont>();
        var seen = new HashSet<FontFile>();
        foreach (var family in families)
        foreach (var file in family.Files)
        {
            if (!seen.Add(file)) continue;
            if (Read(file) is not { } bytes)
            {
                logger?.LogWarning(
                    "The face '{Family}' names the resource '{Resource}' in '{Assembly}', and there is no such resource: the file is not served, and text set in it falls back.",
                    family.Name, file.Resource, file.Assembly);
                continue;
            }
            var extension = Path.GetExtension(file.Resource).ToLowerInvariant();
            fonts.Add(new ServedFont(family, file, Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + extension, bytes));
        }
        return new ServedFonts(fonts, theme.TextFamily);
    }

    private static IEnumerable<FontFamily> ThemeFamilies(IAppTheme theme)
    {
        if (theme.TextFamily is { } text) yield return text;
        if (theme.MonoFamily is { } mono) yield return mono;
        foreach (var role in Enum.GetValues<TypeRole>())
            if (theme.Type(role).Family is { } family) yield return family;
    }

    private static IEnumerable<FontFamily> CatalogFamilies(IEnumerable<Assembly> assemblies) => assemblies
        .Distinct()
        .SelectMany(assembly => assembly.GetCustomAttributes<FontCatalogAttribute>())
        .SelectMany(catalog => catalog.Catalog.GetProperties(BindingFlags.Public | BindingFlags.Static))
        .Where(property => property.PropertyType == typeof(FontFamily))
        .Select(property => property.GetValue(null))
        .OfType<FontFamily>();

    /// <summary>The file's bytes from the assembly it is embedded in, or null when either is not there.</summary>
    private static byte[]? Read(FontFile file)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(new AssemblyName(file.Assembly));
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
        using var stream = assembly.GetManifestResourceStream(file.Resource);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
