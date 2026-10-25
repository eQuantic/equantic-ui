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
/// is read once from the assembly it is embedded in and served at a URL addressed by its content, so the
/// routes serve only what was named and have no path to walk.
/// <list type="bullet">
/// <item>The theme's stylesheet declares every family the theme names (its text face, its code face and
/// every role's face) and every family in the catalogs of the assemblies the app scans (#1055). Naming a
/// family is what ships it, so nothing lists them twice.</item>
/// <item>A page declares in its head every other family its text names (#1056), so a family built by hand
/// or read from a library's catalog loads as the theme's do.</item>
/// <item>A browser building a page asks for a file by its identity, an assembly and a resource, and is
/// sent to the file's content-addressed URL (<see cref="Locate"/>).</item>
/// </list>
/// </summary>
internal sealed class ServedFonts
{
    /// <summary>
    /// The prefix an eQuantic build embeds a face under, which both SDKs' targets write, and the only one a
    /// browser may ask for a file by its identity under.
    /// </summary>
    public const string EmbeddedPrefix = "eQuantic.UI.Fonts/";

    private readonly ConcurrentDictionary<string, ServedFont> _byName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Assembly, string Resource), Lazy<ServedFont?>> _byFile = new();
    private readonly ConcurrentDictionary<FontFamily, Lazy<string>> _pageRules = new();
    private readonly HashSet<FontFamily> _sheet;
    private readonly ILogger? _logger;

    private ServedFonts(IReadOnlyList<FontFamily> sheet, FontFamily? text, ILogger? logger)
    {
        _logger = logger;
        _sheet = [.. sheet];
        var faces = new List<(FontFamily Family, FontFile File, ServedFont Font)>();
        foreach (var family in sheet)
            if (Serve(family) is { } served)
                faces.AddRange(family.Files.Zip(served, (file, font) => (family, file, font)));
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

    /// <summary>The file served under <paramref name="name"/>, or null for a name nothing was served under.</summary>
    public ServedFont? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>
    /// The faces <paramref name="theme"/> and the catalogs of <paramref name="assemblies"/> ship, which the
    /// theme's stylesheet declares. A family one of whose files is not there is left out, and said once to
    /// <paramref name="logger"/>.
    /// </summary>
    public static ServedFonts Of(IAppTheme theme, IEnumerable<Assembly> assemblies, ILogger? logger) =>
        new(ThemeFamilies(theme).Concat(CatalogFamilies(assemblies))
            .Where(family => family.Files.Count > 0)
            .Distinct()
            .ToList(), theme.TextFamily, logger);

    /// <summary>
    /// The <c>@font-face</c> rules a page writes in its head for the families its text named (#1056):
    /// each one the theme's stylesheet does not declare, its files served from here on. A family one of
    /// whose files cannot be read is declared on no page, and said once.
    /// </summary>
    public string RulesFor(IEnumerable<FontFamily> families) => string.Concat(families
        .Where(family => family.Files.Count > 0 && !_sheet.Contains(family))
        .Distinct()
        .Select(family => _pageRules.GetOrAdd(family, named => new Lazy<string>(() =>
            Serve(named) is { } served
                ? FontFaceRules.Write(named.Files.Zip(served, (file, font) => (named, file, font.Url)))
                : "")).Value));

    /// <summary>
    /// The file a browser asks for by its identity (#1056): the resource <paramref name="resource"/> of the
    /// assembly named <paramref name="assembly"/>, read the first time it is asked for, or null. Only an
    /// assembly the app has loaded is read, never one this would have to load, and only a resource an
    /// eQuantic build embedded as a face, under <see cref="EmbeddedPrefix"/>: an assembly may embed a font
    /// for something else, a PDF writer's, whose licence does not cover the web, and a browser asking is
    /// no reason to publish it. A name nothing embeds is not remembered, so asking cannot grow the set.
    /// </summary>
    public ServedFont? Locate(string assembly, string resource)
    {
        if (!resource.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
            || !(resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || resource.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
            return null;
        // A file served already is answered from the set, without walking the assemblies again.
        if (_byFile.TryGetValue((assembly, resource), out var known) && known.Value is { } served) return served;
        var holder = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(loaded => !loaded.IsDynamic && loaded.GetName().Name == assembly);
        if (holder is null || !holder.GetManifestResourceNames().Contains(resource, StringComparer.Ordinal)) return null;
        return Read(assembly, resource, () => Bytes(holder, resource));
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
            var font = new ServedFont(resource,
                Convert.ToHexStringLower(SHA256.HashData(read))[..16] + Path.GetExtension(resource).ToLowerInvariant(),
                read, Compressed(read));
            _byName.TryAdd(font.Name, font);
            return font;
        })).Value;

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
}
