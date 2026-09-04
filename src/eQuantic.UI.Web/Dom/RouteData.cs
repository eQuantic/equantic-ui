using System.Collections.Generic;

namespace eQuantic.UI.Web;

/// <summary>
/// Route data available during rendering — the matched route parameters and the URL query string.
/// On the server this is populated per request from the HTTP route values + query; on the client the
/// runtime fills it from the matched route + URL. Pages read it through <see cref="RenderContext.Route"/>
/// (e.g. <c>context.Route.Param("id")</c>), which the compiler transpiles to the runtime's
/// <c>context.route.param("id")</c>.
/// </summary>
public class RouteData
{
    private readonly Dictionary<string, string> _params;
    private readonly Dictionary<string, string> _query;

    public RouteData()
        : this(new Dictionary<string, string>(), new Dictionary<string, string>())
    {
    }

    public RouteData(Dictionary<string, string> @params, Dictionary<string, string> query)
    {
        _params = @params;
        _query = query;
    }

    /// <summary>The value of a route parameter (e.g. <c>id</c> in <c>/users/{id}</c>), or null.</summary>
    public string? Param(string name) => _params.TryGetValue(name, out var value) ? value : null;

    /// <summary>The value of a query-string field, or null.</summary>
    public string? Query(string name) => _query.TryGetValue(name, out var value) ? value : null;

    /// <summary>All route parameters, by name.</summary>
    public IReadOnlyDictionary<string, string> Params => _params;

    /// <summary>All query-string fields, by name — the pair of <see cref="Params"/>.</summary>
    public IReadOnlyDictionary<string, string> QueryValues => _query;
}
