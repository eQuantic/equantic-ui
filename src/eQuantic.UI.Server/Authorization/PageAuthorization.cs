using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// A page's authorization, as endpoint metadata on the route that serves it (#673).
/// <para>
/// <c>[Authorize]</c> on a <c>[Page]</c> held for its Server Actions only: the route served anyone,
/// and its <c>IServerPrefetch</c> ran for an anonymous visitor and wrote its fields into the HTML.
/// ASP.NET Core's own pages and Blazor read the requirement from the endpoint's metadata, which is
/// what <c>RequireAuthorization</c> sets, so the page's route carries it there and the app's
/// authorization middleware decides with the schemes and policies the app configured: a challenge
/// for an anonymous full load, a 403 for a visitor without the policy, and the page's handler, its
/// prefetch included, never runs for a refused request.
/// </para>
/// <para>
/// The SDK's <see cref="Primitives.AuthorizeAttribute"/> and <see cref="Primitives.AllowAnonymousAttribute"/>
/// are translated into ASP.NET Core's (the vocabulary has no dependencies to implement its
/// interfaces with), and an attribute that already is ASP.NET Core's passes through as it is.
/// </para>
/// </summary>
internal static class PageAuthorization
{
    /// <summary>The authorization metadata of a page type, in the shapes the middleware reads.</summary>
    public static object[] MetadataFor(Type pageType)
    {
        var metadata = new List<object>();
        foreach (var attribute in pageType.GetCustomAttributes(inherit: true))
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
        return [.. metadata];
    }

    /// <summary>
    /// Whether the request may see a page it reaches WITHOUT the page's own route: the 404 page the
    /// fallback draws for a URL that matches nothing, and the 500 page that stands in for one that
    /// failed. No endpoint carries their requirement there, so it is asked here, of the app's own
    /// policy provider and evaluator, before the page is built (found by Copilot on #686). A page that
    /// requires authorization where the app configured none is refused, never drawn.
    /// </summary>
    public static async Task<bool> AllowsAsync(HttpContext context, Type pageType)
    {
        var metadata = MetadataFor(pageType);
        if (metadata.OfType<IAllowAnonymous>().Any()) return true;
        var requirements = metadata.OfType<IAuthorizeData>().ToArray();
        if (requirements.Length == 0) return true;

        var provider = context.RequestServices.GetService<IAuthorizationPolicyProvider>();
        var evaluator = context.RequestServices.GetService<IPolicyEvaluator>();
        if (provider is null || evaluator is null) return false;
        var policy = await AuthorizationPolicy.CombineAsync(provider, requirements);
        if (policy is null) return true;
        var authentication = await evaluator.AuthenticateAsync(policy, context);
        return (await evaluator.AuthorizeAsync(policy, authentication, context, resource: null)).Succeeded;
    }

    /// <summary>The page's requirement on the endpoint that serves it.</summary>
    public static TBuilder WithPageAuthorization<TBuilder>(this TBuilder builder, Type pageType)
        where TBuilder : IEndpointConventionBuilder =>
        MetadataFor(pageType) is { Length: > 0 } metadata ? builder.WithMetadata(metadata) : builder;
}
