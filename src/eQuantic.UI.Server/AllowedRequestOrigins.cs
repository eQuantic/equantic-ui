using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// Which requests the endpoints a page sends to take, by where the browser says they came from: the
/// rule of <see cref="ServerActionOrigins"/> over the origins <c>EQuantic:ServerActions:AllowedOrigins</c>
/// and <see cref="UIOptions.AllowServerActionOrigins"/> allow, as the configuration says NOW. The Server
/// Actions and the uploads ask it (#1214), so a page on another domain the app allows reaches both, and
/// another site's page reaches neither.
/// <para>
/// A reloaded <c>appsettings.json</c> changes the origins without a restart. The change is subscribed
/// to BEFORE the first read, so a reload between the two is heard, and each refresh reads the latest
/// value under one lock, so two refreshes racing cannot leave the older list in place.
/// </para>
/// </summary>
internal sealed class AllowedRequestOrigins
{
    private readonly object _gate = new();

    // Volatile: a reloaded configuration replaces the allow-list on its own thread while requests read it.
    private volatile ServerActionOrigins _origins;

    public AllowedRequestOrigins(IOptionsMonitor<ServerActionsOptions> actions)
    {
        actions.OnChange(_ => Refresh(actions));
        Refresh(actions);
    }

    /// <summary>Whether the endpoint takes <paramref name="request"/>.</summary>
    public bool Allows(HttpRequest request) => _origins.Allows(request);

    /// <summary>
    /// A header as a log line takes it. The request writes it, so a CR or an LF in it would otherwise
    /// start a line of its own in a plain-text log.
    /// </summary>
    public static string ForLog(string value) =>
        value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    [MemberNotNull(nameof(_origins))]
    private void Refresh(IOptionsMonitor<ServerActionsOptions> actions)
    {
        lock (_gate)
            _origins = new ServerActionOrigins(actions.CurrentValue.AllowedOrigins);
    }
}
