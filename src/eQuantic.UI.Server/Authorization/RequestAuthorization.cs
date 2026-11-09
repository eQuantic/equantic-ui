using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// The one rule a page and a Server Action are authorized by: their attributes read as ASP.NET Core's
/// authorization middleware reads an endpoint's, and decided by the app's own policy provider and
/// evaluator (#1285).
/// <para>
/// A Server Action was authorized by a rule of its own, which read the SDK's <c>[Authorize]</c> only,
/// checked roles against the default scheme's user, and let an action that named no requirement run
/// for anyone: ASP.NET Core's <c>[Authorize]</c> on a page refused a navigation and ran the page's
/// actions for an anonymous POST, a page that required another scheme ran its actions for the default
/// scheme's user, and the app's <c>FallbackPolicy</c> was never asked.
/// </para>
/// </summary>
internal static class RequestAuthorization
{
    /// <summary>
    /// The authorization metadata a type or a method carries, in the shapes the middleware reads. The SDK's
    /// <see cref="Primitives.AuthorizeAttribute"/> and <see cref="Primitives.AllowAnonymousAttribute"/> are
    /// translated into ASP.NET Core's (the vocabulary has no dependencies to implement its interfaces
    /// with), and an attribute that already is ASP.NET Core's passes through as it is. Every one is read,
    /// two <c>[Authorize]</c> on one member included.
    /// </summary>
    public static List<object> MetadataOf(MemberInfo member)
    {
        var metadata = new List<object>();
        foreach (var attribute in member.GetCustomAttributes(inherit: true))
        {
            switch (attribute)
            {
                case Primitives.AuthorizeAttribute authorize:
                    metadata.Add(new AuthorizeAttribute
                    {
                        Policy = authorize.Policy,
                        Roles = authorize.Roles,
                        AuthenticationSchemes = authorize.AuthenticationSchemes,
                    });
                    break;
                case Primitives.AllowAnonymousAttribute:
                    metadata.Add(new AllowAnonymousAttribute());
                    break;
                case IAuthorizeData or IAllowAnonymous:
                    metadata.Add(attribute);
                    break;
            }
        }
        return metadata;
    }

    /// <summary>
    /// Whether the request may reach what carries <paramref name="metadata"/>, decided as the authorization
    /// middleware decides an endpoint: the requirements combined into one policy, the app's
    /// <c>FallbackPolicy</c> standing for none, the policy's schemes authenticated (the request's principal
    /// becomes the one they answer, an <c>[AllowAnonymous]</c> one's included), then its requirements, with
    /// the request as the resource. A requirement the app registered no authorization for is refused, never
    /// waved through.
    /// </summary>
    public static async Task<PolicyAuthorizationResult> DecideAsync(HttpContext context, IReadOnlyList<object> metadata)
    {
        var requirements = metadata.OfType<IAuthorizeData>().ToArray();
        var anonymous = metadata.OfType<IAllowAnonymous>().Any();

        var provider = context.RequestServices.GetService<IAuthorizationPolicyProvider>();
        var evaluator = context.RequestServices.GetService<IPolicyEvaluator>();
        if (provider is null || evaluator is null)
            return anonymous || requirements.Length == 0 ? PolicyAuthorizationResult.Success() : PolicyAuthorizationResult.Forbid();

        // No requirement combines into the fallback policy, or into none when the app set none.
        var policy = await AuthorizationPolicy.CombineAsync(provider, requirements);
        if (policy is null) return PolicyAuthorizationResult.Success();
        var authentication = await evaluator.AuthenticateAsync(policy, context);
        if (anonymous) return PolicyAuthorizationResult.Success();
        return await evaluator.AuthorizeAsync(policy, authentication, context, resource: context);
    }
}
