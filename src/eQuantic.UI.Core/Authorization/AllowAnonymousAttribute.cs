using System;

namespace eQuantic.UI.Core.Authorization;

/// <summary>
/// Specifies that the Server Action does not require authorization.
/// Use this attribute to override class-level <see cref="AuthorizeAttribute"/> for specific methods.
/// </summary>
/// <remarks>
/// This attribute takes precedence over <see cref="AuthorizeAttribute"/>.
/// When applied to a method, that Server Action can be invoked without authentication,
/// even if the containing class has <see cref="AuthorizeAttribute"/> applied.
/// </remarks>
/// <example>
/// <code>
/// [Authorize] // All actions in this component require auth by default
/// public class UserProfileComponent : StatefulComponent
/// {
///     [ServerAction]
///     public async Task UpdateProfile(string name) { } // Requires auth
///
///     [ServerAction]
///     [AllowAnonymous] // This action is public
///     public async Task GetPublicInfo() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class AllowAnonymousAttribute : Attribute
{
}
