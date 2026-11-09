using System.Collections.Concurrent;
using System.Reflection;
using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The dashboard sample, hosted from its OWN <c>Program</c>: the same theme, the same localization
/// middleware, the same shell. A rebuilt lookalike would be a second app to keep in step, and the
/// faults <see cref="DashboardDocumentTests"/> exists for only show in the document that goes over
/// the wire.
/// <para>
/// The sample writes no <c>public partial class Program</c> for this: ASP.NET Core 10's own
/// generator declares one for an app of top-level statements, which is what lets a test name it.
/// </para>
/// </summary>
public sealed class DashboardSite : WebApplicationFactory<Program>
{
    /// <summary>
    /// Every route the sample DECLARES, read off the same <see cref="PageAttribute"/> the server's own
    /// scan reads. Never a list in this file: a list goes stale the moment a screen is added, and the
    /// screen missing from it is precisely the one nobody checked.
    /// </summary>
    public static readonly string[] Routes = Declared(parameterised: false);

    /// <summary>
    /// Routes with a <c>{parameter}</c>, which need a value this test cannot invent. None today.
    /// Counted rather than dropped, so the first one to appear says so in a failure message instead
    /// of quietly shrinking what is checked.
    /// </summary>
    public static readonly string[] Parameterised = Declared(parameterised: true);

    private static string[] Declared(bool parameterised) =>
        typeof(Program).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<PageAttribute>())
            .Select(page => page.Route)
            .Where(route => route.Contains('{') == parameterised)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

    // Each route renders once for every test of the fixture: /table and /sheet build hundreds of
    // cells, and the fragment test asks again for the pages its links point at.
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _documents = new(StringComparer.Ordinal);

    /// <summary>
    /// Production, as the app ships and as scripts/capture-readme.sh photographs it: Development adds
    /// a hot reload connection to every page, which is markup no visitor is ever served.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");

    /// <summary>The document <paramref name="route"/> serves, and a failure when it serves none.</summary>
    public Task<string> DocumentAsync(string route) =>
        _documents.GetOrAdd(route, key => new Lazy<Task<string>>(() => FetchAsync(key))).Value;

    private async Task<string> FetchAsync(string route)
    {
        var response = await CreateClient().GetAsync(route);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
