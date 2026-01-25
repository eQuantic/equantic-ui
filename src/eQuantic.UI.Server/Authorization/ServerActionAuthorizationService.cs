using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Use aliases to avoid ambiguity with Microsoft.AspNetCore.Authorization
using AuthorizeAttribute = eQuantic.UI.Core.Authorization.AuthorizeAttribute;
using AllowAnonymousAttribute = eQuantic.UI.Core.Authorization.AllowAnonymousAttribute;
using IAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;

namespace eQuantic.UI.Server.Authorization;

/// <summary>
/// Default implementation of <see cref="IServerActionAuthorizationService"/>.
/// Integrates with ASP.NET Core's authorization infrastructure.
/// </summary>
public class ServerActionAuthorizationService : IServerActionAuthorizationService
{
    private readonly IAuthorizationService? _authorizationService;
    private readonly ILogger<ServerActionAuthorizationService> _logger;

    public ServerActionAuthorizationService(
        IServiceProvider serviceProvider,
        ILogger<ServerActionAuthorizationService> logger)
    {
        // IAuthorizationService is optional - may not be configured in simple apps
        _authorizationService = serviceProvider.GetService<IAuthorizationService>();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServerActionAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        ServerActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var method = descriptor.Method;
        var componentType = descriptor.ComponentType;

        // Check for [AllowAnonymous] on method - takes precedence over everything
        if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            _logger.LogDebug(
                "Server Action {ActionId} allows anonymous access (method-level)",
                descriptor.ActionId);
            return ServerActionAuthorizationResult.Success();
        }

        // Check for [AllowAnonymous] on class (only if no method-level [Authorize])
        var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();
        if (methodAuthorize == null &&
            componentType.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            _logger.LogDebug(
                "Server Action {ActionId} allows anonymous access (class-level)",
                descriptor.ActionId);
            return ServerActionAuthorizationResult.Success();
        }

        // Collect authorization requirements
        var authorizeAttributes = GetAuthorizeAttributes(method, componentType);

        // If no authorization attributes, action is public by default
        if (!authorizeAttributes.Any())
        {
            _logger.LogDebug(
                "Server Action {ActionId} has no authorization requirements",
                descriptor.ActionId);
            return ServerActionAuthorizationResult.Success();
        }

        // From here, authorization is required
        var user = context.User;

        // Check authentication first
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning(
                "Unauthorized access attempt to Server Action {ActionId} - user not authenticated",
                descriptor.ActionId);
            return ServerActionAuthorizationResult.Unauthenticated();
        }

        // Process each authorization attribute
        foreach (var attr in authorizeAttributes)
        {
            var result = await EvaluateAuthorizeAttributeAsync(context, user, attr, descriptor.ActionId);
            if (!result.Succeeded)
            {
                return result;
            }
        }

        _logger.LogDebug(
            "Server Action {ActionId} authorized for user {UserId}",
            descriptor.ActionId,
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? "unknown");

        return ServerActionAuthorizationResult.Success();
    }

    private static AuthorizeAttribute[] GetAuthorizeAttributes(MethodInfo method, Type componentType)
    {
        // Method-level attributes take precedence and are combined with class-level
        var methodAttributes = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
        var classAttributes = componentType.GetCustomAttributes<AuthorizeAttribute>().ToArray();

        // If method has specific attributes, use those combined with class attributes
        // This allows additive authorization (class requires auth, method requires specific role)
        return methodAttributes.Concat(classAttributes).Distinct().ToArray();
    }

    private async Task<ServerActionAuthorizationResult> EvaluateAuthorizeAttributeAsync(
        HttpContext context,
        ClaimsPrincipal user,
        AuthorizeAttribute attribute,
        string actionId)
    {
        // Check roles
        if (!string.IsNullOrWhiteSpace(attribute.Roles))
        {
            var roles = attribute.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var hasRole = roles.Any(role => user.IsInRole(role));

            if (!hasRole)
            {
                _logger.LogWarning(
                    "Access denied to Server Action {ActionId} - user lacks required roles: {Roles}",
                    actionId,
                    attribute.Roles);
                return ServerActionAuthorizationResult.RolesFailed(attribute.Roles);
            }
        }

        // Check policy
        if (!string.IsNullOrWhiteSpace(attribute.Policy))
        {
            if (_authorizationService == null)
            {
                _logger.LogError(
                    "Authorization policy '{Policy}' specified for Server Action {ActionId} but IAuthorizationService is not available. " +
                    "Ensure services.AddAuthorization() is called.",
                    attribute.Policy,
                    actionId);
                return ServerActionAuthorizationResult.Forbidden(
                    $"Authorization service not configured for policy '{attribute.Policy}'.");
            }

            var policyResult = await _authorizationService.AuthorizeAsync(user, null, attribute.Policy);
            if (!policyResult.Succeeded)
            {
                _logger.LogWarning(
                    "Access denied to Server Action {ActionId} - policy '{Policy}' not satisfied",
                    actionId,
                    attribute.Policy);
                return ServerActionAuthorizationResult.PolicyFailed(attribute.Policy);
            }
        }

        return ServerActionAuthorizationResult.Success();
    }
}
