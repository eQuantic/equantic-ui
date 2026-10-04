using System.Reflection;
using System.Runtime.CompilerServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server.Client;

/// <summary>
/// What the shell tells every page about the app as a whole, worked out ONCE per <see cref="UIOptions"/>:
/// the client's route table and whether any component declares a server action. Both walked every type
/// of every scanned assembly, the second every method of every type, on each page the shell served,
/// for answers that cannot change while the app runs. The routes are read on the first page served,
/// after every route has been mapped.
/// </summary>
internal sealed class AppSurface
{
    private static readonly ConditionalWeakTable<UIOptions, AppSurface> Surfaces = new();

    private AppSurface(IReadOnlyList<ClientRoute> routes, bool hasServerActions)
    {
        Routes = routes;
        HasServerActions = hasServerActions;
    }

    /// <summary>The client's route table: every <c>[Page]</c> and <c>MapPage</c> route, with its
    /// language-prefixed twins, and the title the router gives the document.</summary>
    public IReadOnlyList<ClientRoute> Routes { get; }

    /// <summary>Whether any scanned type declares a <c>[ServerAction]</c>.</summary>
    public bool HasServerActions { get; }

    /// <summary>The app's surface, computed on the first call for these options.</summary>
    public static AppSurface Of(UIOptions options, Func<string, IEnumerable<string>> clientPatterns) =>
        Surfaces.GetValue(options, _ => new AppSurface(RoutesOf(options, clientPatterns), HasActions(options)));

    private static List<ClientRoute> RoutesOf(UIOptions options, Func<string, IEnumerable<string>> clientPatterns) =>
        options.AssembliesToScan
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetCustomAttributes<PageAttribute>()
                .Select(attribute => (Pattern: attribute.Route, Page: type.Name, attribute.Title)))
            .Concat(options.DeclaredRoutes.Select(route => (route.Pattern, Page: route.Page.Name, route.Title)))
            // The prefixed URLs go in the table too, or the FIRST client-side navigation inside a
            // translated page finds no match and falls back to a full reload — the language would
            // survive and the SPA would not, which is the kind of regression nobody reports.
            .SelectMany(route => clientPatterns(route.Pattern).Select(pattern => (Pattern: pattern, route.Page, route.Title)))
            .Distinct()
            .Select(route => new ClientRoute(route.Pattern, route.Page, string.IsNullOrEmpty(route.Title) ? null : route.Title))
            .ToList();

    private static bool HasActions(UIOptions options) =>
        options.AssembliesToScan
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods())
            .Any(method => method.GetCustomAttributes(typeof(ServerActionAttribute), false).Any());
}
