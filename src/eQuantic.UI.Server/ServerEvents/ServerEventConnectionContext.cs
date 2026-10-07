using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>A page's connection as a <see cref="IServerEventHandler"/> hears it open and close.</summary>
/// <param name="ConnectionId">The connection's id.</param>
/// <param name="HttpContext">The request that holds the connection open.</param>
public sealed record ServerEventConnectionContext(string ConnectionId, HttpContext HttpContext);
