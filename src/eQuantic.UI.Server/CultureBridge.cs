using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// Track L D4 — the culture half of the shell bridge, the theme bridge's shape slot for slot:
/// the server picks the catalog the request's UI culture resolves to and inlines it as
/// <c>window.__EQ_CULTURE__ = { name, formatName, strings }</c>, applied by boot BEFORE hydration
/// so the client resolves exactly the strings the server rendered. The catalogs are the build's
/// own output (<c>wwwroot/_equantic/strings/{culture}.json</c>, eqc-emitted): resolution walks
/// exact culture → parents → <c>neutral.json</c> — the .NET fallback chain, already FLATTENED at
/// emit time (D12), so this walk only picks a FILE and never merges.
/// </summary>
internal static class CultureBridge
{
    /// <summary>Per-path content cache, invalidated by write time — a translation edit shows on
    /// the next request in dev without a restart, and production never re-reads a static file.</summary>
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, string Text)> Cache = new();

    internal static string? BuildCultureData(HttpContext context, CultureInfo uiCulture, CultureInfo formatCulture)
    {
        var webRoot = context.RequestServices.GetService<IWebHostEnvironment>()?.WebRootPath;
        if (string.IsNullOrEmpty(webRoot)) webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var stringsDir = Path.Combine(webRoot, "_equantic", "strings");
        if (!Directory.Exists(stringsDir)) return null;

        var catalog = PickCatalog(stringsDir, uiCulture);
        if (catalog is null) return null;

        // The catalog file is the build's own System.Text.Json output (default encoder escapes
        // '<', '>' and '&'), so inlining it RAW inside a <script> is safe by construction; the
        // culture names still serialize through the encoder because they are request-shaped.
        return $"{{\"name\":{JsonSerializer.Serialize(uiCulture.Name)}," +
            $"\"formatName\":{JsonSerializer.Serialize(formatCulture.Name)}," +
            $"\"calendar\":{CalendarJson(formatCulture)}," +
            $"\"strings\":{catalog}}}";
    }

    /// <summary>
    /// What a calendar is CALLED for this request's format culture — shipped, not derived. The
    /// browser's ICU and .NET's do not always agree (ar-EG abbreviates Sunday as "أحد" here and
    /// "الأحد" in a JS runtime, both correct Arabic), and a day name that differs between the SSR
    /// HTML and the hydrated tree is a flicker on exactly the pages nobody debugs. The client
    /// keeps an Intl fallback for renders with no server behind them.
    /// </summary>
    private static string CalendarJson(CultureInfo formatCulture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = formatCulture;
            return JsonSerializer.Serialize(new
            {
                firstDayOfWeek = CalendarNames.FirstDayOfWeek,
                dayNamesShort = CalendarNames.DayNamesShort,
                dayNamesLong = CalendarNames.DayNamesLong,
                monthNames = CalendarNames.MonthNames,
                monthNamesShort = CalendarNames.MonthNamesShort,
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
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
