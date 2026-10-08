using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// A topic as one request meets it: what a topic's authorization decides on, and what a
/// <see cref="IServerEventHandler"/> hears when the topic is bound to a connection or released. An
/// authorization policy's handlers receive it as their resource.
/// </summary>
/// <param name="ConnectionId">The connection the topic is bound to.</param>
/// <param name="Topic">The topic's name.</param>
/// <param name="Values">The values of the template that matched, by parameter name.</param>
/// <param name="HttpContext">The request: the user, the cookies, the services.</param>
public sealed record ServerTopicContext(
    string ConnectionId,
    string Topic,
    IReadOnlyDictionary<string, string?> Values,
    HttpContext HttpContext);
