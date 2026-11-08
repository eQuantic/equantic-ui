using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server;

/// <summary>
/// The faces the web serves, one set for the app, which the render and the font routes share. What a page
/// names is served at a URL every instance of the app answers alike, whatever requests it has answered: a
/// page one instance rendered and a font another is asked for resolve alike.
/// <list type="bullet">
/// <item>The theme's stylesheet declares every family the theme names (its text face, its code face and
/// every role's face) and every family in the catalogs of the assemblies the app scans (#1055), each file
/// at a URL addressed by its content. Naming a family is what ships it, so nothing lists them twice.</item>
/// <item>A page declares in its head every other family its text names (#1056), when every file of it is a
/// face an eQuantic build embedded or a file the theme's stylesheet serves. A face outside the stylesheet is
/// served at its IDENTITY, its assembly and its resource, with the hash of its bytes beside them
/// (<see cref="VersionedUrl"/>), which any instance answers by reading the file as <see cref="FontFile.Read"/>
/// does (#1080).</item>
/// <item>A browser building a page asks for a file by its identity alone, and is sent to the URL the server
/// writes for it (<see cref="Locate"/>).</item>
/// </list>
/// No instance has to know which faces the deployment carries. Until #1080 every one read that set from the
/// assemblies the host lists for the deployment and a walk through their references, and a single-file app
/// lists none of the assemblies its bundle carries: a family that named its assembly only as text left no
/// reference to follow, and was declared on no page.
/// </summary>
internal sealed class ServedFonts
{
    /// <summary>
    /// The prefix an eQuantic build embeds a face under, which both SDKs' targets write: the faces outside
    /// the theme's stylesheet the web serves, and the only ones a browser may ask for by their identity.
    /// </summary>
    public const string EmbeddedPrefix = "eQuantic.UI.Fonts/";

    /// <summary>The route a face is served under by its identity, the prefix of <see cref="VersionedUrl"/>.</summary>
    public const string IdentityRoute = "/_equantic/fonts/";

    private readonly Dictionary<string, ServedFont> _sheetByName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Assembly, string Resource), Lazy<ServedFont?>> _byFile = new();
    private readonly ConcurrentDictionary<FontFamily, Lazy<string>> _pageRules = new();
    private readonly HashSet<FontFamily> _sheet;
    private readonly HashSet<(string Assembly, string Resource)> _sheetFiles = [];
    private readonly ILogger? _logger;

    private ServedFonts(IReadOnlyList<FontFamily> sheet, FontFamily? text, ILogger? logger)
    {
        _logger = logger;
        _sheet = [.. sheet];
        var faces = new List<(FontFamily Family, FontFile File, ServedFont Font)>();
        foreach (var family in sheet)
        {
            if (Serve(family) is not { } served) continue;
            faces.AddRange(family.Files.Zip(served, (file, font) => (family, file, font)));
            foreach (var file in family.Files) _sheetFiles.Add((file.Assembly, file.Resource));
        }
        Fonts = faces.Select(face => face.Font).Distinct().ToList();
        foreach (var font in Fonts) _sheetByName.TryAdd(font.Name, font);
        Rules = FontFaceRules.Write(faces.Select(face => (face.Family, face.File, face.Font.Url)));
        // The text face's upright files set nearly every glyph of every page, so their fetch starts
        // from the head. Every other face loads when text first uses it: a preload a page does not use
        // costs its bytes and delays the ones it does.
        Preloads = string.Join("\n        ", faces
            .Where(face => text is not null && face.Family.Equals(text) && !face.File.Italic)
            .Select(face => face.Font)
            .Distinct()
            .Select(font => $"<link rel=\"preload\" href=\"{font.Url}\" as=\"font\" type=\"{font.ContentType}\" crossorigin>"));
    }

    /// <summary>Every file the theme's stylesheet declares, in the order its rules declare them.</summary>
    public IReadOnlyList<ServedFont> Fonts { get; }

    /// <summary>The <c>@font-face</c> rules, written into the theme's stylesheet.</summary>
    public string Rules { get; }

    /// <summary>The head's preloads for the text face's upright files.</summary>
    public string Preloads { get; }

    /// <summary>
    /// The file the theme's stylesheet serves under <paramref name="name"/>, or null: every instance reads the
    /// stylesheet's files when it starts, so every one answers the same names.
    /// </summary>
    public ServedFont? Find(string name) => _sheetByName.GetValueOrDefault(name);

    /// <summary>
    /// The faces <paramref name="theme"/> and the catalogs of <paramref name="assemblies"/> ship, which the
    /// theme's stylesheet declares. A family one of whose files is not there is left out, and said once to
    /// <paramref name="logger"/>.
    /// </summary>
    public static ServedFonts Of(IAppTheme theme, IEnumerable<Assembly> assemblies, ILogger? logger)
    {
        var roots = assemblies.Append(Assembly.GetEntryAssembly()).OfType<Assembly>().Distinct().ToList();
        return new(ThemeFamilies(theme).Concat(CatalogFamilies(roots))
            .Where(family => family.Files.Count > 0)
            .Distinct()
            .ToList(), theme.TextFamily, logger);
    }

    /// <summary>
    /// The <c>@font-face</c> rules a page writes in its head for the families its text named (#1056): each
    /// one the theme's stylesheet does not declare and the web serves, its files served from here on. A
    /// family one of whose files is not served is declared on no page, and said once.
    /// </summary>
    public string RulesFor(IEnumerable<FontFamily> families) => string.Concat(families
        .Where(family => family.Files.Count > 0 && !_sheet.Contains(family))
        .Distinct()
        .Select(family => _pageRules.GetOrAdd(family, named => new Lazy<string>(() => PageRules(named))).Value));

    /// <summary>
    /// The file a browser asks for by its identity (#1056): a file the theme's stylesheet serves, which a
    /// page may name in a family of its own, or the face an eQuantic build embedded as
    /// <paramref name="resource"/> in the assembly named <paramref name="assembly"/>, read from it as
    /// <see cref="FontFile.Read"/> reads it, and null for anything else. An assembly may embed a font for
    /// something else, a PDF writer's, whose licence does not cover the web, and a browser asking is no reason
    /// to publish it; nor is a platform assembly loaded because a URL named it.
    /// </summary>
    public ServedFont? Locate(string assembly, string resource) =>
        _sheetFiles.Contains((assembly, resource)) || (IsEmbeddedFace(resource) && IsAppAssembly(assembly))
            ? Read(assembly, resource, () => new FontFile(assembly, resource, 400, 400).Read())
            : null;

    /// <summary>
    /// Where a page fetches the file <paramref name="font"/> read from <paramref name="resource"/> of
    /// <paramref name="assembly"/>: the stylesheet's own URL for a file the stylesheet serves, so every page
    /// shares one cached copy of it, and otherwise its identity with the hash of its bytes beside it, the shape
    /// <c>asp-append-version</c> writes, which any instance answers (#1080).
    /// </summary>
    public string VersionedUrl(string assembly, string resource, ServedFont font) =>
        _sheetFiles.Contains((assembly, resource))
            ? font.Url
            : $"{IdentityRoute}{Uri.EscapeDataString(assembly)}/{string.Join('/', resource.Split('/').Select(Uri.EscapeDataString))}?v={font.Version}";

    /// <summary>
    /// The rules of a family a page names, when the web serves every file of it: one the theme's
    /// stylesheet serves, or a face an eQuantic build embedded, which every instance of the app reads and
    /// serves alike.
    /// </summary>
    private string PageRules(FontFamily family)
    {
        var served = new List<string>(family.Files.Count);
        foreach (var file in family.Files)
        {
            if (Locate(file.Assembly, file.Resource) is not { } font)
            {
                _logger?.LogWarning(
                    "The face '{Family}' names the resource '{Resource}' in '{Assembly}', which no eQuantic build embedded as a face and the theme does not name, or which is not there: the web does not serve it, so text set in it falls back there.",
                    family.Name, file.Resource, file.Assembly);
                return "";
            }
            served.Add(VersionedUrl(file.Assembly, file.Resource, font));
        }
        return FontFaceRules.Write(family.Files.Zip(served, (file, url) => (family, file, url)));
    }

    /// <summary>
    /// The served files of <paramref name="family"/>, in its order, or null when one cannot be read: WHOLE
    /// or not at all, as every shell resolves a family, so its text falls back on the web as on Photon
    /// rather than drawing one cut from it and another from the platform.
    /// </summary>
    private IReadOnlyList<ServedFont>? Serve(FontFamily family)
    {
        var served = new List<ServedFont>(family.Files.Count);
        foreach (var file in family.Files)
        {
            if (Read(file.Assembly, file.Resource, file.Read) is not { } font)
            {
                _logger?.LogWarning(
                    "The face '{Family}' names the resource '{Resource}' in '{Assembly}', and there is no such resource: the family is not served, and text set in it falls back.",
                    family.Name, file.Resource, file.Assembly);
                return null;
            }
            served.Add(font);
        }
        return served;
    }

    /// <summary>The file embedded as <paramref name="resource"/> in <paramref name="assembly"/>, read and
    /// compressed once, whichever path names it first.</summary>
    private ServedFont? Read(string assembly, string resource, Func<byte[]?> bytes) =>
        _byFile.GetOrAdd((assembly, resource), _ => new Lazy<ServedFont?>(() =>
            bytes() is { } read ? new ServedFont(resource, NameOf(resource, read), read, Compressed(read)) : null)).Value;

    /// <summary>The name a file is served under: a hash of its bytes, and its extension.</summary>
    private static string NameOf(string resource, byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + Path.GetExtension(resource).ToLowerInvariant();

    /// <summary>Whether <paramref name="resource"/> names a face an eQuantic build embedded.</summary>
    private static bool IsEmbeddedFace(string resource) =>
        resource.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
        && (resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || resource.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether <paramref name="name"/> is the simple name of an assembly an app ships, the only kind a URL
    /// may have loaded: nothing but letters, digits, dots, dashes and underscores, so no version, culture or
    /// key rides in, and none of the platform's own assemblies, which embed no face an app ships.
    /// </summary>
    private static bool IsAppAssembly(string name) =>
        name.Length > 0
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')
        && name is not ("netstandard" or "mscorlib" or "System")
        && !name.StartsWith("System.", StringComparison.Ordinal)
        && !name.StartsWith("Microsoft.", StringComparison.Ordinal);

    private static byte[] Compressed(byte[] bytes)
    {
        using var buffer = new MemoryStream();
        using (var brotli = new BrotliStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            brotli.Write(bytes);
        return buffer.ToArray();
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
}
