using System.Text.Json;

namespace eQuantic.UI.Server.Client;

/// <summary>
/// The configuration the shell hands the client as <c>window.__EQ_CONFIG</c>, read by the boot
/// (<c>Resources/boot.ts</c>): the page this document drew, the build it came from, whether it was
/// rendered on the server, whether the server streams rebuilds (<see cref="UIOptions.HotReloads(Microsoft.Extensions.Hosting.IHostEnvironment)"/>),
/// the theme cookie, the language-prefix policy and the route table.
/// Written by System.Text.Json alone (<see cref="Json"/>), so no string in it is quoted by hand.
/// </summary>
internal sealed record ClientConfig(
    string? Page,
    string Version,
    bool Ssr,
    bool HotReload,
    object ThemeCookie,
    ClientCultureRoutes? CultureRoutes,
    IReadOnlyList<ClientRoute> Routes)
{
    /// <summary>
    /// camelCase names, as the boot reads them, and the default encoder, which escapes every code
    /// unit a script element cannot carry raw: a line terminator, a control, a quote, and the
    /// <c>&lt;</c> of a <c>&lt;/script&gt;</c>.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
