using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
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

        if (rule.Rule.RequiresAuthenticatedUser || rule.Rule.Policies.Count > 0)
        {
            // As ASP.NET Core's authorization middleware evaluates an endpoint's: the policies combined,
            // their authentication schemes authenticated (the request's principal becomes the one they
            // answer), then their requirements, with the topic as the resource. Evaluating the
            // requirements alone against the request's principal let the default scheme's user meet
            // a policy restricted to another scheme, a bearer token's for one (#647). No authorization
            // registered means no policy can be met: refused, not waved through.
            var provider = http.RequestServices.GetService<IAuthorizationPolicyProvider>();
            var evaluator = http.RequestServices.GetService<IPolicyEvaluator>();
            if (provider is null || evaluator is null) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
            var policy = await PolicyOf(rule.Rule, provider);
            var authenticated = await evaluator.AuthenticateAsync(policy, http);
            if (!(await evaluator.AuthorizeAsync(policy, authenticated, http, context)).Succeeded)
                return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
        }

        foreach (var authorize in rule.Rule.Delegates)
        {
            if (!await authorize(context)) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
        }

        return ServerTopicAuthorization.Allowed(context);
    }

    /// <summary>The rule's policies as one, the app's default policy standing for a rule that asks only
    /// for a signed-in user, as <c>RequireAuthorization()</c> on an endpoint does.</summary>
    private static async Task<AuthorizationPolicy> PolicyOf(ServerTopicRule rule, IAuthorizationPolicyProvider provider)
    {
        var combined = new AuthorizationPolicyBuilder();
        if (rule.RequiresAuthenticatedUser) combined.Combine(await provider.GetDefaultPolicyAsync());
        foreach (var name in rule.Policies)
        {
            combined.Combine(await provider.GetPolicyAsync(name)
                             ?? throw new InvalidOperationException(
                                 $"The topic template '{rule.Template}' requires the authorization policy '{name}', and the app registers none of that name."));
        }
        return combined.Build();
    }
}
