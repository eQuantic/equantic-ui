using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server;

/// <summary>
/// The faces the web serves, one set for the app, which the render and the font routes share. Each file
/// is served at a URL addressed by its content, and what the set serves is decided by the app's
/// deployment alone, never by the requests an instance has answered: a page one instance of the app
/// rendered and a font another is asked for resolve alike.
/// <list type="bullet">
/// <item>The theme's stylesheet declares every family the theme names (its text face, its code face and
/// every role's face) and every family in the catalogs of the assemblies the app scans (#1055). Naming a
/// family is what ships it, so nothing lists them twice.</item>
/// <item>A page declares in its head every other family its text names (#1056), when every file of it is
/// a face an eQuantic build embedded in the app's assemblies, or a file the theme's stylesheet serves.</item>
/// <item>A browser building a page asks for a file by its identity, an assembly and a resource, and is
/// sent to the file's content-addressed URL (<see cref="Locate"/>).</item>
/// </list>
/// </summary>
internal sealed class ServedFonts
{
    /// <summary>
    /// The prefix an eQuantic build embeds a face under, which both SDKs' targets write: the faces outside
    /// the theme's stylesheet the web serves, and the only ones a browser may ask for by their identity.
    /// </summary>
    public const string EmbeddedPrefix = "eQuantic.UI.Fonts/";

    private readonly ConcurrentDictionary<string, ServedFont> _byName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Assembly, string Resource), Lazy<ServedFont?>> _byFile = new();
    private readonly ConcurrentDictionary<FontFamily, Lazy<string>> _pageRules = new();
    private readonly HashSet<FontFamily> _sheet;
    private readonly HashSet<(string Assembly, string Resource)> _sheetFiles = [];
    private readonly Lazy<EmbeddedFaces> _embedded;
    private readonly ILogger? _logger;

    private ServedFonts(IReadOnlyList<FontFamily> sheet, FontFamily? text, IReadOnlyList<Assembly> roots, ILogger? logger)
    {
        _logger = logger;
        _sheet = [.. sheet];
        _embedded = new Lazy<EmbeddedFaces>(() => EmbeddedFaces.Of(roots));
        var faces = new List<(FontFamily Family, FontFile File, ServedFont Font)>();
        foreach (var family in sheet)
        {
            if (Serve(family) is not { } served) continue;
            faces.AddRange(family.Files.Zip(served, (file, font) => (family, file, font)));
            foreach (var file in family.Files) _sheetFiles.Add((file.Assembly, file.Resource));
        }
        Fonts = faces.Select(face => face.Font).Distinct().ToList();
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
    /// The file served under <paramref name="name"/>, or null for a name nothing is served under: one the
    /// theme's stylesheet serves, or a face an eQuantic build embedded in the app's assemblies, which any
    /// instance answers whether or not it rendered the page that named it.
    /// </summary>
    public ServedFont? Find(string name) =>
        _byName.TryGetValue(name, out var font) ? font
        : _embedded.Value.Named(name) is { } embedded ? Read(embedded.Assembly, embedded.Resource, () => Bytes(embedded.Holder, embedded.Resource))
        : null;

    /// <summary>
    /// The faces <paramref name="theme"/> and the catalogs of <paramref name="assemblies"/> ship, which the
    /// theme's stylesheet declares, with the app's assemblies the faces outside it are read from. A family
    /// one of whose files is not there is left out, and said once to <paramref name="logger"/>.
    /// </summary>
    public static ServedFonts Of(IAppTheme theme, IEnumerable<Assembly> assemblies, ILogger? logger)
    {
        var roots = assemblies.Append(Assembly.GetEntryAssembly()).OfType<Assembly>().Distinct().ToList();
        return new(ThemeFamilies(theme).Concat(CatalogFamilies(roots))
            .Where(family => family.Files.Count > 0)
            .Distinct()
            .ToList(), theme.TextFamily, roots, logger);
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
    /// The file a browser asks for by its identity (#1056): the face an eQuantic build embedded as
    /// <paramref name="resource"/> in the app's assembly named <paramref name="assembly"/>, or null for
    /// anything else. An assembly may embed a font for something else, a PDF writer's, whose licence does
    /// not cover the web, and a browser asking is no reason to publish it.
    /// </summary>
    public ServedFont? Locate(string assembly, string resource) =>
        IsEmbeddedFace(resource) && _embedded.Value.Of(assembly, resource) is { } embedded
            ? Read(assembly, resource, () => Bytes(embedded.Holder, resource))
            : null;

    /// <summary>
    /// The rules of a family a page names, when the web serves every file of it: one the theme's
    /// stylesheet serves, or a face an eQuantic build embedded in the app's assemblies, which every
    /// instance of the app serves alike. A file only the instance that read it would serve is a URL
    /// another instance answers with 404.
    /// </summary>
    private string PageRules(FontFamily family)
    {
        if (family.Files.FirstOrDefault(file => !_sheetFiles.Contains((file.Assembly, file.Resource))
                && _embedded.Value.Of(file.Assembly, file.Resource) is null) is { } other)
        {
            _logger?.LogWarning(
                "The face '{Family}' names the resource '{Resource}' in '{Assembly}', which no eQuantic build embedded as a face and the theme does not name: the web does not serve it, so text set in it falls back there, and it loads on Photon.",
                family.Name, other.Resource, other.Assembly);
            return "";
        }
        return Serve(family) is { } served
            ? FontFaceRules.Write(family.Files.Zip(served, (file, font) => (family, file, font.Url)))
            : "";
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
        {
            if (bytes() is not { } read) return null;
            var font = new ServedFont(resource, NameOf(resource, read), read, Compressed(read));
            _byName.TryAdd(font.Name, font);
            return font;
        })).Value;

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

    /// <summary>
    /// Every face an eQuantic build embedded in the app's own assemblies: the ones it scans, the entry
    /// assembly, and every assembly they reference outside the platform, each read once. Taken from the
    /// app's deployment alone, the first time a face outside the theme's stylesheet is asked for, so every
    /// instance of the app knows the same faces whatever it has served.
    /// </summary>
    private sealed class EmbeddedFaces
    {
        private readonly Dictionary<(string Assembly, string Resource), (Assembly Holder, string Name)> _byIdentity = new();
        private readonly Dictionary<string, (Assembly Holder, string Assembly, string Resource)> _byName = new(StringComparer.Ordinal);

        /// <summary>The face embedded as <paramref name="resource"/> in the assembly named <paramref name="assembly"/>.</summary>
        public (Assembly Holder, string Name)? Of(string assembly, string resource) =>
            _byIdentity.TryGetValue((assembly, resource), out var face) ? face : null;

        /// <summary>The face served under <paramref name="name"/>.</summary>
        public (Assembly Holder, string Assembly, string Resource)? Named(string name) =>
            _byName.TryGetValue(name, out var face) ? face : null;

        public static EmbeddedFaces Of(IEnumerable<Assembly> roots)
        {
            var faces = new EmbeddedFaces();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<Assembly>(roots.Where(root => !root.IsDynamic));
            while (pending.TryDequeue(out var assembly))
            {
                if (assembly.GetName().Name is not { } name || !visited.Add(name)) continue;
                foreach (var resource in assembly.GetManifestResourceNames().Where(IsEmbeddedFace))
                {
                    if (Bytes(assembly, resource) is not { } bytes) continue;
                    var served = NameOf(resource, bytes);
                    faces._byIdentity[(name, resource)] = (assembly, served);
                    faces._byName.TryAdd(served, (assembly, name, resource));
                }
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Name is not { } referenced || visited.Contains(referenced) || IsPlatform(referenced)) continue;
                    try
                    {
                        pending.Enqueue(Assembly.Load(reference));
                    }
                    catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
                    {
                        // A reference the deployment does not carry has no face to give.
                    }
                }
            }
            return faces;
        }

        /// <summary>The platform's own assemblies embed no face an app ships, and walking them would load
        /// the whole framework.</summary>
        private static bool IsPlatform(string name) =>
            name is "netstandard" or "mscorlib" or "System"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
