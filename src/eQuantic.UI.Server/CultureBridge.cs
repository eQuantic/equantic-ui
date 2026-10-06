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
/// <c>window.__EQ_CULTURE__ = { name, formatName, calendar, format, strings }</c>, applied by boot
/// BEFORE hydration, so the client writes every number and date as the server wrote them and
/// resolves exactly the strings it rendered. The FORMAT culture travels with every page
/// (<c>format</c>, from .NET's own data — see <see cref="Web.CultureFormatBridge"/> — and
/// <c>calendar</c>, what its calendar is called — see <see cref="CalendarJson"/>), whether or not the
/// app has a single string to translate: a page with no catalog was handed no culture at all, so the
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
            $"\"calendar\":{CalendarJson(formatCulture)}," +
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(formatCulture)}" +
            (catalog is null ? "" : $",\"strings\":{catalog}") + "}";
    }

    /// <summary>
    /// A format culture's half of the bridge, for a page that switches to it with no reload: what it
    /// formats with and what its calendar is called, written as the shell writes them for a request
    /// in that culture. Null for a name that is no culture .NET knows.
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
            $"\"calendar\":{CalendarJson(culture)}," +
            $"\"format\":{Web.CultureFormatBridge.SerializeJson(culture)}}}";
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
