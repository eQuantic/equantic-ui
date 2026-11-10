using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server;

/// <summary>
/// The faces the web serves, one set for the app, which the render and the font routes share. What it
/// serves is decided by the app's deployment alone, never by the requests an instance has answered: a page
/// one instance of the app rendered and a font another is asked for resolve alike.
/// <list type="bullet">
/// <item>The theme's stylesheet declares every family the theme names (its text face, its code face and
/// every role's face) and every family in the catalogs of the assemblies the app scans (#1055), each file at
/// a URL addressed by its content. Naming a family is what ships it, so nothing lists them twice.</item>
/// <item>A page declares in its head every other family its text names (#1056), when every file of it is
/// a face an eQuantic build embedded, or a file the theme's stylesheet serves. A face outside the
/// stylesheet is served at a URL that carries its identity, an assembly and a resource, with the hash of
/// its bytes beside them, so any instance answers it by loading the assembly by its name, as Photon reads
/// it, and no instance needs to know which faces the deployment carries: a single-file app's host lists
/// none of the assemblies its bundle carries, and a family may name its assembly only as text (#1080).</item>
/// <item>A browser building a page asks for a file by its identity alone, and is sent to the URL a page
/// the server rendered names it by (<see cref="Locate"/>).</item>
/// </list>
/// </summary>
internal sealed class ServedFonts
{
    /// <summary>
    /// The prefix an eQuantic build embeds a face under, which both SDKs' targets write: the faces outside
    /// the theme's stylesheet the web serves, and the only ones a browser may ask for by their identity.
    /// </summary>
    public const string EmbeddedPrefix = "eQuantic.UI.Fonts/";

    private readonly Dictionary<string, ServedFont> _byName = new(StringComparer.Ordinal);
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
        foreach (var font in Fonts) _byName.TryAdd(font.Name, font);
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
    /// The file the theme's stylesheet serves under <paramref name="name"/>, or null: the set every instance
    /// reads from the theme when it starts, whatever pages it rendered.
    /// </summary>
    public ServedFont? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>
    /// The faces <paramref name="theme"/> and the catalogs of <paramref name="assemblies"/> ship, which the
    /// theme's stylesheet declares. A family one of whose files is not there is left out, and said once to
    /// <paramref name="logger"/>.
    /// </summary>
    public static ServedFonts Of(IAppTheme theme, IEnumerable<Assembly> assemblies, ILogger? logger) =>
        new(ThemeFamilies(theme).Concat(CatalogFamilies(assemblies.Append(Assembly.GetEntryAssembly()).OfType<Assembly>()))
            .Where(family => family.Files.Count > 0)
            .Distinct()
            .ToList(), theme.TextFamily, logger);

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
    /// The file a browser asks for by its identity, with the URL a page names it by: a file the theme's
    /// stylesheet serves, at its content-addressed URL, which a page may name in a family of its own, or the
    /// face an eQuantic build embedded as <paramref name="resource"/> in the assembly named
    /// <paramref name="assembly"/>, at its identity, and null for anything else. An assembly may embed a
    /// font for something else, a PDF writer's, whose licence does not cover the web, and a browser asking
    /// is no reason to publish it.
    /// </summary>
    public (ServedFont Font, string Url)? Locate(string assembly, string resource)
    {
        if (_sheetFiles.Contains((assembly, resource)))
            return Read(assembly, resource, () => null) is { } sheet ? (sheet, sheet.Url) : null;
        return Holder(assembly, resource) is { } holder && Read(assembly, resource, () => Bytes(holder, resource)) is { } font
            ? (font, IdentityUrl(assembly, resource, font))
            : null;
    }

    /// <summary>
    /// The rules of a family a page names, when the web serves every file of it: one the theme's
    /// stylesheet serves, or a face an eQuantic build embedded, each at the URL every instance of the app
    /// answers. A face no eQuantic build embedded is one the app never chose to publish.
    /// </summary>
    private string PageRules(FontFamily family)
    {
        var faces = new List<(FontFamily Family, FontFile File, string Url)>(family.Files.Count);
        foreach (var file in family.Files)
        {
            if (!_sheetFiles.Contains((file.Assembly, file.Resource)) && !IsEmbeddedFace(file.Resource))
            {
                _logger?.LogWarning(
                    "The face '{Family}' names the resource '{Resource}' in '{Assembly}', which no eQuantic build embedded as a face and the theme does not name: the web does not serve it, so text set in it falls back there, and it loads on Photon.",
                    family.Name, file.Resource, file.Assembly);
                return "";
            }
            // WHOLE or not at all, as every shell resolves a family.
            if (Locate(file.Assembly, file.Resource) is not { } located)
            {
                _logger?.LogWarning(
                    "The face '{Family}' names the resource '{Resource}' in '{Assembly}', and no assembly of exactly that name outside the platform's holds it in the app's deployment: the family is not served, and text set in it falls back.",
                    family.Name, file.Resource, file.Assembly);
                return "";
            }
            faces.Add((family, file, located.Url));
        }
        return FontFaceRules.Write(faces);
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

    /// <summary>
    /// The URL of a face outside the theme's stylesheet: its identity, which any instance answers, and the
    /// hash of its bytes, the shape <c>asp-append-version</c> writes, so a browser keeps it for as long as
    /// it lasts and a new build's file is a new URL.
    /// </summary>
    private static string IdentityUrl(string assembly, string resource, ServedFont font) =>
        "/_equantic/fonts/" + Uri.EscapeDataString(assembly) + "/"
        + string.Join('/', resource.Split('/').Select(Uri.EscapeDataString)) + "?v=" + font.Version;

    /// <summary>
    /// The assembly named <paramref name="assembly"/> when <paramref name="resource"/> is a face an eQuantic
    /// build embedded in it, or null. The name is loaded as <see cref="FontFile.Read"/> loads it, so any
    /// assembly the app's deployment carries answers, a single-file app's bundle included, and only a
    /// simple name: never the platform's own assemblies, a path or a display name, and only an assembly
    /// of exactly that name, so one file is read and cached once, however a request spells it.
    /// </summary>
    private static Assembly? Holder(string assembly, string resource)
    {
        if (!IsEmbeddedFace(resource) || IsPlatform(assembly) || assembly.IndexOfAny(['/', '\\']) >= 0) return null;
        try
        {
            var name = new AssemblyName(assembly);
            return name.Name == assembly && Assembly.Load(name) is var holder && holder.GetName().Name == assembly
                && holder.GetManifestResourceInfo(resource) is not null ? holder : null;
        }
        catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>The name a file is served under: a hash of its bytes, and its extension.</summary>
    private static string NameOf(string resource, byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes))[..16] + Path.GetExtension(resource).ToLowerInvariant();

    /// <summary>Whether <paramref name="resource"/> names a face an eQuantic build embedded.</summary>
    private static bool IsEmbeddedFace(string resource) =>
        resource.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
        && (resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || resource.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

    private static byte[]? Bytes(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

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

    /// <summary>The platform's own assemblies embed no face an app ships, and a name a browser sends never
    /// loads one.</summary>
    private static bool IsPlatform(string name) =>
        name is "netstandard" or "mscorlib" or "System"
        || name.StartsWith("System.", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.", StringComparison.Ordinal);
}
