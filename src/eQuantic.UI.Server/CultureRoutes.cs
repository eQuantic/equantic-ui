using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// The language as the FIRST PATH SEGMENT — <c>/pt-BR/pricing</c> — declared once and honoured by
/// the three places that have to agree about it: the endpoints a page is reachable at, the culture
/// the request renders in, and the href every in-app link carries.
/// <para>
/// The DEFAULT culture is unprefixed, so <c>/pricing</c> stays exactly where it was. That is not a
/// stylistic preference: a site that moves every existing URL behind a prefix asks a search engine
/// to relearn the whole thing, and the redirect that keeps the old ones alive is a hop on every
/// visit forever.
/// </para>
/// <para>
/// A prefix the app never declared is not a culture: the <c>culture</c> route constraint rejects
/// it, nothing matches, and the request is a 404. Serving English under <c>/fr/</c> would let a
/// crawler index a French URL that is not French.
/// </para>
/// </summary>
/// <param name="Default">The culture served WITHOUT a prefix.</param>
/// <param name="Prefixed">The cultures that appear as a leading path segment.</param>
public sealed record CultureRouteMap(string Default, IReadOnlyList<string> Prefixed)
{
    /// <summary>Builds the map from a list whose FIRST entry is the unprefixed default.</summary>
    public static CultureRouteMap From(IReadOnlyList<string> cultures) =>
        cultures.Count == 0
            ? throw new ArgumentException("Name at least one culture — the first is the unprefixed default.", nameof(cultures))
            : new CultureRouteMap(cultures[0], [.. cultures.Skip(1)]);

    /// <summary>Every culture, the default first — the list <c>RequestLocalization</c> supports.</summary>
    public IReadOnlyList<string> All => [Default, .. Prefixed];

    /// <summary>The prefix segment for a culture, or empty for the default — <c>"pt-BR"</c>,
    /// <c>""</c>. Case-insensitive, because a URL a person typed is not a URL a link produced.</summary>
    public string SegmentFor(string culture) =>
        Prefixed.FirstOrDefault(p => string.Equals(p, culture, StringComparison.OrdinalIgnoreCase)) ?? "";

    /// <summary>Whether this is the default culture — the one served without a prefix.</summary>
    public bool IsDefault(string culture) => string.Equals(culture, Default, StringComparison.OrdinalIgnoreCase);

    /// <summary>The culture a path is asking for, and the path with that segment removed.
    /// <c>/pt-BR/pricing</c> → <c>("pt-BR", "/pricing")</c>; <c>/pricing</c> → <c>(Default, "/pricing")</c>.</summary>
    public (string Culture, string Path) Split(string path)
    {
        var trimmed = path.StartsWith('/') ? path[1..] : path;
        var slash = trimmed.IndexOf('/');
        var head = slash < 0 ? trimmed : trimmed[..slash];
        var match = Prefixed.FirstOrDefault(p => string.Equals(p, head, StringComparison.OrdinalIgnoreCase));
        if (match is null) return (Default, path.Length == 0 ? "/" : path);
        var rest = slash < 0 ? "/" : trimmed[slash..];
        return (match, rest);
    }

    /// <summary>The same page in another language. Idempotent: a path that already carries a
    /// prefix has it REPLACED, never stacked, which is what makes it safe to call on a link whose
    /// destination the caller did not write.</summary>
    public string PathFor(string culture, string path)
    {
        var (_, rest) = Split(path);
        var segment = SegmentFor(culture);
        if (segment.Length == 0) return rest;
        return rest == "/" ? "/" + segment : "/" + segment + rest;
    }
}

/// <summary>
/// The <c>culture</c> route constraint — what makes <c>/{culture:culture}/pricing</c> match
/// <c>/pt-BR/pricing</c> and nothing else. ASP.NET's own extension point for "this segment is one
/// of these": registered in <c>RouteOptions.ConstraintMap</c> by <see cref="UIOptions.UseCultureRoutes"/>,
/// resolved by the framework's inline constraint resolver, and consulted by endpoint routing
/// before any handler runs. Only the PREFIXED cultures match: the default has no segment.
/// </summary>
public sealed class CultureRouteConstraint(UIOptions options) : IRouteConstraint
{
    /// <summary>The name the pattern uses: <c>{culture:culture}</c>.</summary>
    public const string Name = "culture";

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (options.CultureRoutes is not { } map) return false;
        if (!values.TryGetValue(routeKey, out var value) || value is null) return false;
        var segment = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return map.SegmentFor(segment).Length > 0;
    }
}

/// <summary>
/// The platform wiring behind <see cref="UIOptions.UseCultureRoutes"/>: nothing here is the SDK's
/// own — it is ASP.NET Core's <c>RequestLocalizationOptions</c> filled from the one list the app
/// wrote, its <c>RouteDataRequestCultureProvider</c> put FIRST, and its constraint map taught the
/// word <c>culture</c>.
/// </summary>
public static class CultureRouteExtensions
{
    /// <summary>
    /// The explicit form, for an app that configures the middleware with the lambda overload
    /// (<c>app.UseRequestLocalization(o => o.UseCultureRoutes(languages))</c>) — that overload builds
    /// its options inline and sees nothing registered through DI, so the same list goes here. An
    /// app that calls <c>app.UseRequestLocalization()</c> bare needs nothing: <see cref="UIOptions.UseCultureRoutes"/>
    /// already configured the options it reads.
    /// </summary>
    public static RequestLocalizationOptions UseCultureRoutes(this RequestLocalizationOptions options, params string[] cultures) =>
        options.UseCultureRoutes(CultureRouteMap.From(cultures));

    internal static RequestLocalizationOptions UseCultureRoutes(this RequestLocalizationOptions options, CultureRouteMap map)
    {
        ArgumentNullException.ThrowIfNull(options);
        var all = map.All.ToArray();
        options.SetDefaultCulture(map.Default)
               .AddSupportedCultures(all)
               .AddSupportedUICultures(all);

        // FIRST, ahead of the cookie and Accept-Language, and that ordering is the whole point: a
        // URL naming a language is a promise about what the page says. Letting a cookie win would
        // serve Spanish at /pt-BR/pricing — to the reader, to a crawler, and to whoever the link
        // was sent to. The provider is the platform's: it reads the `culture` route value the
        // constrained pattern binds, and answers nothing for a bare URL, where the others decide.
        if (!options.RequestCultureProviders.Any(provider => provider is RouteDataRequestCultureProvider))
            options.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
        return options;
    }

    /// <summary>What <c>AddUI</c> registers when the app declared language prefixes.</summary>
    internal static IServiceCollection AddCultureRoutes(this IServiceCollection services, CultureRouteMap map)
    {
        services.Configure<RequestLocalizationOptions>(options => options.UseCultureRoutes(map));
        services.Configure<RouteOptions>(options =>
            options.ConstraintMap[CultureRouteConstraint.Name] = typeof(CultureRouteConstraint));
        return services;
    }
}
