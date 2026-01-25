using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// Service responsible for authorizing Server Action invocations.
/// </summary>
/// <remarks>
/// This service integrates with ASP.NET Core's authorization infrastructure
/// to validate access to Server Actions based on <see cref="Core.Authorization.AuthorizeAttribute"/>
/// and <see cref="Core.Authorization.AllowAnonymousAttribute"/> decorations.
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
    private ServerActionAuthorizationResult(bool succeeded, string? failureReason = null)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets a value indicating whether authorization succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the reason for authorization failure, if applicable.
    /// </summary>
    public string? FailureReason { get; }

    /// <summary>
    /// Gets a value indicating whether the user is not authenticated.
    /// </summary>
    public bool IsUnauthenticated => FailureReason?.Contains("not authenticated") == true;

    /// <summary>
    /// Gets a value indicating whether the user is authenticated but not authorized.
    /// </summary>
    public bool IsForbidden => !Succeeded && !IsUnauthenticated;

    /// <summary>
    /// Creates a successful authorization result.
    /// </summary>
    public static ServerActionAuthorizationResult Success() => new(true);

    /// <summary>
    /// Creates a failed authorization result due to missing authentication.
    /// </summary>
    public static ServerActionAuthorizationResult Unauthenticated() =>
        new(false, "User is not authenticated.");

    /// <summary>
    /// Creates a failed authorization result due to insufficient permissions.
    /// </summary>
    /// <param name="reason">The specific reason for the authorization failure.</param>
    public static ServerActionAuthorizationResult Forbidden(string reason) =>
        new(false, reason);

    /// <summary>
    /// Creates a failed authorization result due to policy evaluation failure.
    /// </summary>
    /// <param name="policyName">The name of the policy that failed.</param>
    public static ServerActionAuthorizationResult PolicyFailed(string policyName) =>
        new(false, $"Authorization policy '{policyName}' was not satisfied.");

    /// <summary>
    /// Creates a failed authorization result due to missing required roles.
    /// </summary>
    /// <param name="requiredRoles">The roles that were required but not present.</param>
    public static ServerActionAuthorizationResult RolesFailed(string requiredRoles) =>
        new(false, $"User does not have any of the required roles: {requiredRoles}.");
}
