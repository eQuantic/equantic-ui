using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>The server's answer to one subscription: the topic as the request met it, when it is
/// authorized, or why it is not.</summary>
internal readonly record struct ServerTopicAuthorization(ServerTopicContext? Context, ServerTopicRefusalReason? Refusal)
{
    public static ServerTopicAuthorization Allowed(ServerTopicContext context) => new(context, null);

    public static ServerTopicAuthorization Refused(ServerTopicRefusalReason reason) => new(null, reason);
}
