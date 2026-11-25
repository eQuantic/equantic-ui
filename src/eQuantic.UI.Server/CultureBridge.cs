using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Track L D4 — the culture half of the shell bridge, the theme bridge's shape slot for slot:
/// <c>window.__EQ_CULTURE__ = { name, formatName, format, rightToLeft, strings, catalogs }</c>, applied by boot BEFORE
/// hydration, so the client writes every number and date as the server wrote them, names a month as
/// the server named it, and resolves exactly the strings it rendered. The FORMAT culture travels with
/// every page (<c>format</c>, from .NET's own data — see <see cref="Web.CultureFormatBridge"/>, whose
/// day and month names are what a calendar says too), whether or not the app has a single string to
/// translate: a page with no catalog was handed no culture at all, so the
/// browser formatted in its HOST's locale while the server formatted in the request's (#471). The
/// strings travel when there are some: the catalog the request's UI culture resolves to, the build's
/// own output (<c>wwwroot/_equantic/strings/{culture}.json</c>, eqc-emitted), walked exact culture →
/// parents → <c>neutral.json</c> — the .NET fallback chain, already FLATTENED at emit time (D12), so
/// this walk only picks a FILE and never merges. <c>catalogs</c> names the files the build wrote, so a
/// switch in the browser walks the same chain and asks for none that is not there (#825). A culture
/// switch fetches the format half for the culture it switches to from
/// <c>/_equantic/culture/{name}.json</c> (<see cref="FormatDocument"/>). Both say which way the culture's text
/// runs (<c>rightToLeft</c>, the UI culture's in the shell), which a switch writes on the document's
/// <c>dir</c> (#1568).
/// </summary>
internal static class CultureBridge
{
    /// <summary>Per-path content cache, invalidated by write time — a translation edit shows on
    /// the next request in dev without a restart, and production never re-reads a static file.</summary>
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, string Text)> Cache = new();

    /// <summary>The shell's culture data for a request: its UI and format cultures, the page's strings and the
    /// catalogs the build wrote.</summary>
    /// <param name="context">The request.</param>
    /// <param name="uiCulture">The culture the page's strings are in.</param>
    /// <param name="formatCulture">The culture its numbers and dates are written in.</param>
    /// <param name="modules">The modules the page is drawn from (its own and the layout's), whose strings it
    /// ships (#1782); null ships the whole catalog.</param>
    internal static string BuildCultureData(HttpContext context, CultureInfo uiCulture, CultureInfo formatCulture,
        IReadOnlyCollection<string>? modules = null)
    {
        var webRoot = WebRoot(context);
        var stringsDir = Path.Combine(webRoot, "_equantic", "strings");
        var built = Directory.Exists(stringsDir);
        var catalog = built ? PickCatalog(stringsDir, uiCulture, webRoot, modules) : null;
        var written = built ? WrittenCatalogs(stringsDir) : "[]";

        // The catalog file is the build's own System.Text.Json output (default encoder escapes
        // '<', '>' and '&'), so inlining it RAW inside a <script> is safe by construction; the
        // culture names and the format data serialize through the encoder too.
        return $"{{\"name\":{JsonSerializer.Serialize(uiCulture.Name)}," +
            $"\"formatName\":{JsonSerializer.Serialize(formatCulture.Name)}," +
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(formatCulture)}," +
            $"\"rightToLeft\":{RightToLeft(uiCulture)}" +
            (catalog is null ? "" : $",\"strings\":{catalog}") +
            $",\"catalogs\":{written}" + "}";
    }

    /// <summary>The names of the catalogs the build wrote, <c>neutral</c> among them, as JSON, kept by the
    /// folder's write time, which moves when a file comes or goes: every page asks.</summary>
    private static string WrittenCatalogs(string stringsDir)
    {
        var stamp = Directory.GetLastWriteTimeUtc(stringsDir);
        if (Cache.TryGetValue(stringsDir, out var cached) && cached.Stamp == stamp) return cached.Text;
        var names = Directory.EnumerateFiles(stringsDir, "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal);
        var text = JsonSerializer.Serialize(names);
        Cache[stringsDir] = (stamp, text);
        return text;
    }

    /// <summary>
    /// A format culture's half of the bridge, for a page that switches to it with no reload: what it
    /// formats with and what its calendar is called, written as the shell writes it for a request in
    /// that culture. Null for a name that is no culture .NET knows.
    /// </summary>
    internal static string? FormatDocument(string name)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(name, predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
        return $"{{\"formatName\":{JsonSerializer.Serialize(culture.Name)}," +
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(culture)}," +
            $"\"rightToLeft\":{RightToLeft(culture)}}}";
    }

    /// <summary>Whether <paramref name="culture"/>'s text runs right to left, as a JSON literal: .NET's
    /// answer, which the browser takes rather than keeping a list of languages that drifts from ICU.</summary>
    private static string RightToLeft(CultureInfo culture) => culture.TextInfo.IsRightToLeft ? "true" : "false";

    /// <summary>
    /// The strings a client navigation to a page needs, in the request's UI culture (#1782): the page's own,
    /// which the document it navigates from was not handed. Null where the build wrote no catalog.
    /// </summary>
    internal static string? PageStrings(HttpContext context, CultureInfo uiCulture, IReadOnlyCollection<string> modules)
    {
        var webRoot = WebRoot(context);
        var stringsDir = Path.Combine(webRoot, "_equantic", "strings");
        return Directory.Exists(stringsDir) ? PickCatalog(stringsDir, uiCulture, webRoot, modules) : null;
    }

    private static string WebRoot(HttpContext context)
    {
        var webRoot = context.RequestServices.GetService<IWebHostEnvironment>()?.WebRootPath;
        return string.IsNullOrEmpty(webRoot) ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot") : webRoot;
    }

    private static string? PickCatalog(string stringsDir, CultureInfo uiCulture, string webRoot,
        IReadOnlyCollection<string>? modules)
    {
        for (var culture = uiCulture;
             !string.IsNullOrEmpty(culture.Name);
             culture = culture.Parent)
        {
            var path = Path.Combine(stringsDir, culture.Name + ".json");
            if (ReadCached(path) is { } exact) return ForModules(webRoot, path, exact, modules);
        }
        var neutral = Path.Combine(stringsDir, "neutral.json");
        return ReadCached(neutral) is { } text ? ForModules(webRoot, neutral, text, modules) : null;
    }

    /// <summary>The page index the build writes beside the catalogs (eqc's <c>page-strings.json</c>).</summary>
    private sealed record PageIndex(IReadOnlySet<string> Whole, IReadOnlyDictionary<string, string[]> Pages);

    private static readonly ConcurrentDictionary<string, (DateTime Stamp, PageIndex? Index)> Indexes = new();

    private static PageIndex? ReadIndex(string webRoot)
    {
        var path = Path.Combine(webRoot, "_equantic", "page-strings.json");
        try
        {
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Indexes.TryGetValue(path, out var cached) && cached.Stamp == stamp) return cached.Index;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var whole = document.RootElement.TryGetProperty("whole", out var ids)
                ? ids.EnumerateArray().Select(id => id.GetString() ?? "").ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
            var pages = document.RootElement.TryGetProperty("pages", out var entries)
                ? entries.EnumerateObject().ToDictionary(page => page.Name,
                    page => page.Value.EnumerateArray().Select(key => key.GetString() ?? "").ToArray(), StringComparer.Ordinal)
                : new Dictionary<string, string[]>(StringComparer.Ordinal);
            var index = new PageIndex(whole, pages);
            Indexes[path] = (stamp, index);
            return index;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The catalog's strings the page's modules read, and every string of a catalog the build ships whole
    /// (a library's, whose reads it cannot see). The whole catalog where there is no index or the index
    /// does not know one of the modules: a page the build did not index keeps working, and so does a
    /// catalog read from a build before the index existed.
    /// </summary>
    private static string ForModules(string webRoot, string path, string catalog, IReadOnlyCollection<string>? modules)
    {
        if (modules is null || modules.Count == 0) return catalog;
        if (ReadIndex(webRoot) is not { } index || modules.Any(module => !index.Pages.ContainsKey(module))) return catalog;

        var key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}|{string.Join('|', modules.Order(StringComparer.Ordinal))}";
        if (Subsets.TryGetValue(key, out var subset)) return subset;

        var read = modules.SelectMany(module => index.Pages[module]).ToHashSet(StringComparer.Ordinal);
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(catalog) ?? [];
        var shipped = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in strings)
        {
            var slash = name.IndexOf('/');
            if (read.Contains(name) || (slash > 0 && index.Whole.Contains(name[..slash]))) shipped[name] = value;
        }
        // The default encoder escapes '<', '>' and '&' as the build's own output does, so the subset is as
        // safe to inline in a <script> as the file it came from.
        return Subsets[key] = JsonSerializer.Serialize(shipped);
    }

    private static readonly ConcurrentDictionary<string, string> Subsets = new();

    private static string? ReadCached(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (Cache.TryGetValue(path, out var cached) && cached.Stamp == stamp) return cached.Text;
            var text = File.ReadAllText(path);
            Cache[path] = (stamp, text);
            return text;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
