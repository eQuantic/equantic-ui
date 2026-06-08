using Microsoft.AspNetCore.SignalR;

namespace eQuantic.UI.Server.Hubs;

/// <summary>
/// Hub for pushing server-initiated events to connected clients.
/// </summary>
/// <remarks>
/// Broadcasts MUST originate on the server (inject <see cref="IHubContext{ServerActionHub}"/>
/// and call <c>Clients.All.SendAsync(...)</c>). There is deliberately no client-callable
/// relay method: a public <c>SendEvent</c> would let any anonymous client fan out arbitrary
/// content to every other connected client (message injection / spoofing / amplification).
/// </remarks>
public class ServerActionHub : Hub
{
}
