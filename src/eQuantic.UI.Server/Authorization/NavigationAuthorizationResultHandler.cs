using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// How a refused CLIENT NAVIGATION is answered (#673). A navigation is a fetch to the page's own route
/// carrying <c>X-EQ-Navigate</c>, and a fetch cannot follow a scheme's challenge (an OpenID Connect
/// redirect is to another origin), so it is answered 401 when the visitor is not signed in and 403
/// when they are and lack the policy, marked with the header every navigation answer carries. The
/// router then loads the page in full, which the framework's own handling challenges or forbids.
/// <para>
/// Every other request goes to the handler the app had: its own when it registered one before
/// <c>AddUI</c>, the framework's otherwise. One it registers after <c>AddUI</c> replaces this one,
/// and answers a refused navigation itself.
/// </para>
/// </summary>
internal sealed class NavigationAuthorizationResultHandler(IAuthorizationMiddlewareResultHandler inner)
    : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded || !context.Request.Headers.ContainsKey(UIExtensions.NavigationHeader))
            return inner.HandleAsync(next, context, policy, authorizeResult);

        context.Response.StatusCode = authorizeResult.Challenged
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status403Forbidden;
        context.Response.Headers[UIExtensions.NavigationHeader] = "1";
        return Task.CompletedTask;
    }
}
