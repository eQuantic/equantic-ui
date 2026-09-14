using System.Threading;

namespace eQuantic.UI.Primitives;

/// <summary>
/// What the ROUTE said — the matched parameters and the query string, as a write-once page can read
/// them.
/// <para>
/// A page on <c>/docs/{slug}</c> has to know the slug, and until this it could only get there
/// through <c>IHttpContextAccessor</c> — which works, and costs the page its write-once-ness: a
/// component that knows about ASP.NET is a component that cannot run on Photon. This is the same
/// information with no target in it.
/// </para>
/// <para>
/// Never null on <see cref="ComponentContext.Route"/>; an absent name answers null, which is what a
/// caller has to handle anyway (a route can match with an empty optional segment).
/// </para>
/// </summary>
public sealed class RouteValues
{
    /// <summary>The route nothing matched — every lookup answers null.</summary>
    public static readonly RouteValues Empty = new(null, null);

    private readonly IReadOnlyDictionary<string, string>? _params;
    private readonly IReadOnlyDictionary<string, string>? _query;

    public RouteValues(
        IReadOnlyDictionary<string, string>? parameters,
        IReadOnlyDictionary<string, string>? query)
    {
        _params = parameters;
        _query = query;
    }

    /// <summary>A matched route parameter — the <c>slug</c> of <c>/docs/{slug}</c>.</summary>
    public string? Param(string name) =>
        _params is not null && _params.TryGetValue(name, out var value) ? value : null;

    /// <summary>A query-string value — the <c>page</c> of <c>?page=2</c>.</summary>
    public string? Query(string name) =>
        _query is not null && _query.TryGetValue(name, out var value) ? value : null;

    // ---- The AMBIENT route, per request -------------------------------------------------------

    private static readonly AsyncLocal<RouteValues?> Scoped = new();

    /// <summary>
    /// The route for the request being rendered right now.
    /// <para>
    /// AsyncLocal, like every other per-request value here: SSR renders concurrent requests on
    /// shared instances, and a route held in a field would hand one visitor's slug to another's
    /// page. Set by the rendering pipeline and cleared when it finishes.
    /// </para>
    /// </summary>
    public static RouteValues Current
    {
        get => Scoped.Value ?? Empty;
        set => Scoped.Value = value;
    }

    /// <summary>Clears the ambient route — the pipeline's `finally`.</summary>
    public static void ClearCurrent() => Scoped.Value = null;
}
