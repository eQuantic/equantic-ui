using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// Service responsible for authorizing Server Action invocations.
/// </summary>
/// <remarks>
/// The default one (<see cref="ServerActionAuthorizationService"/>) decides an action by the rule a page is
/// decided by: the SDK's <see cref="eQuantic.UI.Primitives.AuthorizeAttribute"/> and ASP.NET Core's
/// <c>[Authorize]</c>, their schemes, and the app's <c>FallbackPolicy</c>.
/// </remarks>
public interface IServerActionAuthorizationService
{
    /// <summary>
    /// Determines whether the current request is authorized to invoke the specified Server Action.
    /// </summary>
    /// <param name="context">The HTTP context containing the current request and user information.</param>
    /// <param name="descriptor">The descriptor of the Server Action being invoked.</param>
    /// <returns>
    /// A <see cref="ServerActionAuthorizationResult"/> indicating whether authorization succeeded
    /// and providing failure details if it did not.
    /// </returns>
    Task<ServerActionAuthorizationResult> AuthorizeAsync(HttpContext context, ServerActionDescriptor descriptor);
}

/// <summary>
/// Represents the result of a Server Action authorization check.
/// </summary>
public sealed class ServerActionAuthorizationResult
{
    private ServerActionAuthorizationResult(bool succeeded, bool unauthenticated, string? failureReason)
    {
        Succeeded = succeeded;
        IsUnauthenticated = unauthenticated;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether authorization succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// What the refusal failed on, for the server's log. It names the roles and policies the action
    /// requires, so it is never written to the response (#1291).
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the caller is not authenticated: the action answers 401.
    /// </summary>
    public bool IsUnauthenticated { get; }

    /// <summary>
    /// Gets a value indicating whether the caller is authenticated but not authorized: the action answers 403.
    /// </summary>
    public bool IsForbidden => !Succeeded && !IsUnauthenticated;

    /// <summary>
    /// Creates a successful authorization result.
    /// </summary>
    public static ServerActionAuthorizationResult Success() => new(true, false, null);

    /// <summary>
    /// Creates a failed authorization result due to missing authentication.
    /// </summary>
    public static ServerActionAuthorizationResult Unauthenticated() =>
        new(false, true, "User is not authenticated.");

    /// <summary>
    /// Creates a failed authorization result due to insufficient permissions.
    /// </summary>
    /// <param name="reason">What the refusal failed on, for the server's log.</param>
    public static ServerActionAuthorizationResult Forbidden(string reason) =>
        new(false, false, reason);
}
