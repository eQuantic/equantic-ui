using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Decides one subscription against the configured templates: the most specific template that
/// matches the topic rules on it, and a topic no template matches is refused. Subscribing fails
/// closed: an app that configured nothing for a topic has not said anyone may hear it.
/// </summary>
internal sealed class ServerTopicAuthorizer(IReadOnlyList<ServerTopicTemplate> templates)
{
    public async ValueTask<ServerTopicAuthorization> AuthorizeAsync(HttpContext http, string connectionId, string topic)
    {
        ServerTopicTemplate? rule = null;
        IReadOnlyDictionary<string, string?> values = new Dictionary<string, string?>();
        foreach (var template in templates)
        {
            if (template.TryMatch(topic, out var matched) && (rule is null || template.Specificity > rule.Specificity))
            {
                rule = template;
                values = matched;
            }
        }

        if (rule is null) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Unknown);

        var context = new ServerTopicContext(connectionId, topic, values, http);
        if (rule.Rule.IsAnonymous) return ServerTopicAuthorization.Allowed(context);

        if (rule.Rule.RequiresAuthenticatedUser && http.User.Identity?.IsAuthenticated != true)
            return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);

        if (rule.Rule.Policies.Count > 0)
        {
            // No authorization registered means no policy can be satisfied: refused, not waved through.
            var authorization = http.RequestServices.GetService<IAuthorizationService>();
            if (authorization is null) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
            foreach (var policy in rule.Rule.Policies)
            {
                if (!(await authorization.AuthorizeAsync(http.User, context, policy)).Succeeded)
                    return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
            }
        }

        foreach (var authorize in rule.Rule.Delegates)
        {
            if (!await authorize(context)) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
        }

        return ServerTopicAuthorization.Allowed(context);
    }
}
