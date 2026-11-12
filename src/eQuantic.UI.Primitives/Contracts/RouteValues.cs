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
/// <para>
/// A component reads it from its context, which reads it from the frame its owner was handed: the server
/// hands its request's, the browser its router's (#1395). Code outside the tree that needs the request's
/// route, a prefetch, resolves it from the request's services, where the server registers it.
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
}
