using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Track L D4 — the culture half of the shell bridge, the theme bridge's shape slot for slot:
/// <c>window.__EQ_CULTURE__ = { name, formatName, format, strings }</c>, applied by boot BEFORE
/// hydration, so the client writes every number and date as the server wrote them, names a month as
/// the server named it, and resolves exactly the strings it rendered. The FORMAT culture travels with
/// every page (<c>format</c>, from .NET's own data — see <see cref="Web.CultureFormatBridge"/>, whose
/// day and month names are what a calendar says too), whether or not the app has a single string to
/// translate: a page with no catalog was handed no culture at all, so the
/// browser formatted in its HOST's locale while the server formatted in the request's (#471). The
/// strings travel when there are some: the catalog the request's UI culture resolves to, the build's
/// own output (<c>wwwroot/_equantic/strings/{culture}.json</c>, eqc-emitted), walked exact culture →
/// parents → <c>neutral.json</c> — the .NET fallback chain, already FLATTENED at emit time (D12), so
/// this walk only picks a FILE and never merges. A culture switch fetches the format half for the
/// culture it switches to from <c>/_equantic/culture/{name}.json</c> (<see cref="FormatDocument"/>).
/// </summary>
internal static class CultureBridge
{
    /// <summary>Per-path content cache, invalidated by write time — a translation edit shows on
    /// the next request in dev without a restart, and production never re-reads a static file.</summary>
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, string Text)> Cache = new();

    internal static string BuildCultureData(HttpContext context, CultureInfo uiCulture, CultureInfo formatCulture)
    {
        var webRoot = context.RequestServices.GetService<IWebHostEnvironment>()?.WebRootPath;
        if (string.IsNullOrEmpty(webRoot)) webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var stringsDir = Path.Combine(webRoot, "_equantic", "strings");
        var catalog = Directory.Exists(stringsDir) ? PickCatalog(stringsDir, uiCulture) : null;

        // The catalog file is the build's own System.Text.Json output (default encoder escapes
        // '<', '>' and '&'), so inlining it RAW inside a <script> is safe by construction; the
        // culture names and the format data serialize through the encoder too.
        return $"{{\"name\":{JsonSerializer.Serialize(uiCulture.Name)}," +
            $"\"formatName\":{JsonSerializer.Serialize(formatCulture.Name)}," +
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(formatCulture)}" +
            (catalog is null ? "" : $",\"strings\":{catalog}") + "}";
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
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(culture)}}}";
    }

    private static string? PickCatalog(string stringsDir, CultureInfo uiCulture)
    {
        for (var culture = uiCulture;
             !string.IsNullOrEmpty(culture.Name);
             culture = culture.Parent)
        {
            if (ReadCached(Path.Combine(stringsDir, culture.Name + ".json")) is { } exact) return exact;
        }
        return ReadCached(Path.Combine(stringsDir, "neutral.json"));
    }

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
