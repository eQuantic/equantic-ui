using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// Default implementation of <see cref="IServerActionAuthorizationService"/>: an action is authorized by
/// the rule a page is (<see cref="RequestAuthorization"/>), over the attributes of its component and of its
/// method, the SDK's and ASP.NET Core's alike.
/// </summary>
/// <remarks>
/// The closest attribute wins: an <c>[AllowAnonymous]</c> on the method opens it whatever its component
/// requires, and an <c>[Authorize]</c> on the method closes it whatever <c>[AllowAnonymous]</c> its
/// component carries, where ASP.NET Core's middleware lets an <c>[AllowAnonymous]</c> anywhere open the
/// endpoint. The two differ only where this one refuses. An action that names no requirement is under the
/// app's <c>FallbackPolicy</c>, as an endpoint is: an app that sets one protects every action it does not
/// open, and one that sets none leaves an action open, as it leaves an endpoint.
/// <para>
/// The page a component is drawn in is not something the request can prove, so an action is not
/// authorized by it: a component that serves a protected page carries the page's requirement itself, or
/// the app's fallback policy covers it.
/// </para>
/// </remarks>
public class ServerActionAuthorizationService(ILogger<ServerActionAuthorizationService> logger)
    : IServerActionAuthorizationService
{
    /// <summary>The metadata each action is decided by, read once: its attributes do not change while the app runs.</summary>
    private static readonly ConcurrentDictionary<(Type Component, MethodInfo Method), IReadOnlyList<object>> Metadata = new();

    /// <inheritdoc />
    public async Task<ServerActionAuthorizationResult> AuthorizeAsync(HttpContext context, ServerActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var metadata = Metadata.GetOrAdd((descriptor.ComponentType, descriptor.Method), key => MetadataOf(key.Component, key.Method));
        var decision = await RequestAuthorization.DecideAsync(context, metadata);
        if (decision.Succeeded)
        {
            logger.LogDebug(
                "Server Action {ActionId} authorized for {User}",
                AllowedRequestOrigins.ForLog(descriptor.ActionId),
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity?.Name ?? "an anonymous caller");
            return ServerActionAuthorizationResult.Success();
        }
        return decision.Challenged
            ? ServerActionAuthorizationResult.Unauthenticated()
            : ServerActionAuthorizationResult.Forbidden(ReasonOf(decision.AuthorizationFailure));
    }

    /// <summary>
    /// The metadata an action is decided by: its component's, then its method's, the method's
    /// <c>[Authorize]</c> setting its component's <c>[AllowAnonymous]</c> aside.
    /// </summary>
    internal static List<object> MetadataOf(Type component, MethodInfo method)
    {
        var own = RequestAuthorization.MetadataOf(method);
        var closes = own.OfType<IAuthorizeData>().Any() && !own.OfType<IAllowAnonymous>().Any();
        var metadata = RequestAuthorization.MetadataOf(component);
        if (closes) metadata.RemoveAll(entry => entry is IAllowAnonymous);
        metadata.AddRange(own);
        return metadata;
    }

    /// <summary>What a refusal failed on, for the server's log: the requirements and the handlers' reasons.</summary>
    private static string ReasonOf(AuthorizationFailure? failure)
    {
        if (failure is null) return "The app registers no authorization services: call AddAuthorization.";
        var reasons = failure.FailedRequirements.Select(requirement => requirement.ToString() ?? requirement.GetType().Name)
            .Concat(failure.FailureReasons.Select(reason => reason.Message))
            .ToArray();
        return reasons.Length > 0 ? string.Join("; ", reasons) : "The policy was not met.";
    }
}
