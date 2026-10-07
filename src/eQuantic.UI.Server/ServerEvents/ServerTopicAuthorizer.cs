using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server;

/// <summary>
/// Decides one subscription against the configured templates: the templates that match the topic and fix
/// the most of it rule on it, each of their rules must allow the subscription, and a topic no template
/// matches is refused. Subscribing fails closed: an app that configured nothing for a topic has not said
/// anyone may hear it, and of two rules for one topic, a library's and the app's, neither opens what the
/// other closes.
/// </summary>
internal sealed class ServerTopicAuthorizer(IReadOnlyList<ServerTopicTemplate> templates)
{
    public async ValueTask<ServerTopicAuthorization> AuthorizeAsync(HttpContext http, string connectionId, string topic)
    {
        // Two templates that fix as much of the topic both rule on it. The first one registered decided
        // alone, so a library's anonymous `prices` hid the app's `prices` that required a signed-in user
        // (Copilot on #647). Their values meet in the topic's context, the first registered keeping a
        // name both give.
        var ruling = new List<ServerTopicRule>();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var specificity = -1;
        foreach (var template in templates)
        {
            if (!template.TryMatch(topic, out var matched) || template.Specificity < specificity) continue;
            if (template.Specificity > specificity)
            {
                ruling.Clear();
                values.Clear();
                specificity = template.Specificity;
            }
            ruling.Add(template.Rule);
            foreach (var (name, value) in matched) values.TryAdd(name, value);
        }

        if (ruling.Count == 0) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Unknown);

        var context = new ServerTopicContext(connectionId, topic, values, http);
        foreach (var rule in ruling)
        {
            if (!await AllowsAsync(rule, context, http)) return ServerTopicAuthorization.Refused(ServerTopicRefusalReason.Forbidden);
        }
        return ServerTopicAuthorization.Allowed(context);
    }

    /// <summary>Whether <paramref name="rule"/> lets the request hear the topic.</summary>
    private static async ValueTask<bool> AllowsAsync(ServerTopicRule rule, ServerTopicContext context, HttpContext http)
    {
        if (rule.IsAnonymous) return true;

        if (rule.RequiresAuthenticatedUser || rule.Policies.Count > 0)
        {
            // As ASP.NET Core's authorization middleware evaluates an endpoint's: the policies combined,
            // their authentication schemes authenticated (the request's principal becomes the one they
            // answer), then their requirements, with the topic as the resource. Evaluating the
            // requirements alone against the request's principal let the default scheme's user meet
            // a policy restricted to another scheme, a bearer token's for one (#647). No authorization
            // registered means no policy can be met: refused, not waved through.
            var provider = http.RequestServices.GetService<IAuthorizationPolicyProvider>();
            var evaluator = http.RequestServices.GetService<IPolicyEvaluator>();
            if (provider is null || evaluator is null) return false;
            var policy = await PolicyOf(rule, provider);
            var authenticated = await evaluator.AuthenticateAsync(policy, http);
            if (!(await evaluator.AuthorizeAsync(policy, authenticated, http, context)).Succeeded) return false;
        }

        foreach (var authorize in rule.Delegates)
        {
            if (!await authorize(context)) return false;
        }
        return true;
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
