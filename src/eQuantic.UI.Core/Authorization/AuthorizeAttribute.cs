using System;

namespace eQuantic.UI.Core.Authorization;

/// <summary>
/// Specifies that the Server Action requires authorization.
/// Can be applied to methods (Server Actions) or classes (Components) to require authentication.
/// </summary>
/// <remarks>
/// When applied to a class, all Server Actions in that class require authorization.
/// When applied to a method, only that specific Server Action requires authorization.
/// Use <see cref="AllowAnonymousAttribute"/> to override class-level authorization for specific methods.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="AuthorizeAttribute"/>.
    /// </summary>
    public AuthorizeAttribute() { }

    /// <summary>
    /// Initializes a new instance of <see cref="AuthorizeAttribute"/> with the specified policy.
    /// </summary>
    /// <param name="policy">The name of the authorization policy to apply.</param>
    public AuthorizeAttribute(string policy)
    {
        Policy = policy;
    }

    /// <summary>
    /// Gets or sets the policy name that determines access to the Server Action.
    /// </summary>
    /// <remarks>
    /// If not specified, only authentication is required (user must be logged in).
    /// If specified, the policy must be registered in ASP.NET Core's authorization services.
    /// </remarks>
    public string? Policy { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of roles that are allowed to access the Server Action.
    /// </summary>
    /// <remarks>
    /// If specified, the user must be in at least one of the listed roles.
    /// Can be combined with <see cref="Policy"/> for more complex authorization scenarios.
    /// </remarks>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets a comma-separated list of authentication schemes to use.
    /// </summary>
    /// <remarks>
    /// If not specified, the default authentication scheme is used.
    /// Multiple schemes can be specified for scenarios requiring specific authentication methods.
    /// </remarks>
    public string? AuthenticationSchemes { get; set; }
}
