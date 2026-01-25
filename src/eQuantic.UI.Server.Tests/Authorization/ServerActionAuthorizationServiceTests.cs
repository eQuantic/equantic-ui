using System.Reflection;
using System.Security.Claims;
using eQuantic.UI.Core;
using eQuantic.UI.Server.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Use our custom attributes explicitly to avoid ambiguity
using AuthorizeAttribute = eQuantic.UI.Core.Authorization.AuthorizeAttribute;
using AllowAnonymousAttribute = eQuantic.UI.Core.Authorization.AllowAnonymousAttribute;
using IAuthorizationService = Microsoft.AspNetCore.Authorization.IAuthorizationService;
using AuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;

namespace eQuantic.UI.Server.Tests.Authorization;

public class ServerActionAuthorizationServiceTests
{
    private readonly ILogger<ServerActionAuthorizationService> _logger =
        NullLogger<ServerActionAuthorizationService>.Instance;

    #region Test Components

    // Component without any authorization
    public class PublicComponent : StatelessComponent
    {
        [ServerAction]
        public void PublicAction() { }

        public override IComponent Build(RenderContext context) => null!;
    }

    // Component with class-level authorization
    [Authorize]
    public class SecureComponent : StatelessComponent
    {
        [ServerAction]
        public void SecureAction() { }

        [ServerAction]
        [AllowAnonymous]
        public void PublicAction() { }

        public override IComponent Build(RenderContext context) => null!;
    }

    // Component with role-based authorization
    public class RoleBasedComponent : StatelessComponent
    {
        [ServerAction]
        [Authorize(Roles = "Admin,Manager")]
        public void AdminAction() { }

        [ServerAction]
        [Authorize(Roles = "User")]
        public void UserAction() { }

        public override IComponent Build(RenderContext context) => null!;
    }

    // Component with policy-based authorization
    public class PolicyBasedComponent : StatelessComponent
    {
        [ServerAction]
        [Authorize(Policy = "CanEditUsers")]
        public void EditUserAction() { }

        public override IComponent Build(RenderContext context) => null!;
    }

    // Component with combined authorization (class + method)
    [Authorize]
    public class CombinedAuthComponent : StatelessComponent
    {
        [ServerAction]
        [Authorize(Roles = "Admin")]
        public void AdminOnlyAction() { }

        public override IComponent Build(RenderContext context) => null!;
    }

    #endregion

    #region Helper Methods

    private static ServerActionDescriptor CreateDescriptor<T>(string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        return new ServerActionDescriptor
        {
            ActionId = $"{typeof(T).Name}/{methodName}",
            ComponentType = typeof(T),
            Method = method,
            ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray(),
            IsAsync = false
        };
    }

    private static HttpContext CreateHttpContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        if (user != null)
        {
            context.User = user;
        }
        return context;
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"User{userId}")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateAnonymousUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    private ServerActionAuthorizationService CreateService(IAuthorizationService? authService = null)
    {
        var services = new ServiceCollection();

        if (authService != null)
        {
            services.AddSingleton(authService);
        }

        var serviceProvider = services.BuildServiceProvider();
        return new ServerActionAuthorizationService(serviceProvider, _logger);
    }

    #endregion

    #region Public Actions (No Authorization)

    [Fact]
    public async Task AuthorizeAsync_PublicAction_AllowsAnonymous()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<PublicComponent>("PublicAction");
        var context = CreateHttpContext(CreateAnonymousUser());

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_PublicAction_AllowsAuthenticated()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<PublicComponent>("PublicAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Class-Level Authorization

    [Fact]
    public async Task AuthorizeAsync_ClassAuthorize_DeniesAnonymous()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<SecureComponent>("SecureAction");
        var context = CreateHttpContext(CreateAnonymousUser());

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsUnauthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_ClassAuthorize_AllowsAuthenticated()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<SecureComponent>("SecureAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_ClassAuthorizeWithAllowAnonymousMethod_AllowsAnonymous()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<SecureComponent>("PublicAction");
        var context = CreateHttpContext(CreateAnonymousUser());

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Role-Based Authorization

    [Fact]
    public async Task AuthorizeAsync_RoleAuthorize_DeniesAnonymous()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<RoleBasedComponent>("AdminAction");
        var context = CreateHttpContext(CreateAnonymousUser());

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsUnauthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_RoleAuthorize_DeniesUserWithoutRole()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<RoleBasedComponent>("AdminAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1", "User")); // Has User role, not Admin

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsForbidden.Should().BeTrue();
        result.FailureReason.Should().Contain("Admin");
    }

    [Fact]
    public async Task AuthorizeAsync_RoleAuthorize_AllowsUserWithRole()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<RoleBasedComponent>("AdminAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1", "Admin"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_RoleAuthorize_AllowsUserWithAnyMatchingRole()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<RoleBasedComponent>("AdminAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1", "Manager")); // Has Manager, which is in "Admin,Manager"

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Policy-Based Authorization

    [Fact]
    public async Task AuthorizeAsync_PolicyAuthorize_DeniesWhenPolicyFails()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        mockAuthService
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var service = CreateService(mockAuthService.Object);
        var descriptor = CreateDescriptor<PolicyBasedComponent>("EditUserAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsForbidden.Should().BeTrue();
        result.FailureReason.Should().Contain("CanEditUsers");
    }

    [Fact]
    public async Task AuthorizeAsync_PolicyAuthorize_AllowsWhenPolicySucceeds()
    {
        // Arrange
        var mockAuthService = new Mock<IAuthorizationService>();
        mockAuthService
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), "CanEditUsers"))
            .ReturnsAsync(AuthorizationResult.Success());

        var service = CreateService(mockAuthService.Object);
        var descriptor = CreateDescriptor<PolicyBasedComponent>("EditUserAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_PolicyAuthorize_FailsWhenAuthorizationServiceNotAvailable()
    {
        // Arrange
        var service = CreateService(authService: null); // No IAuthorizationService
        var descriptor = CreateDescriptor<PolicyBasedComponent>("EditUserAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsForbidden.Should().BeTrue();
        result.FailureReason.Should().Contain("not configured");
    }

    #endregion

    #region Combined Authorization (Class + Method)

    [Fact]
    public async Task AuthorizeAsync_CombinedAuthorize_DeniesAnonymous()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<CombinedAuthComponent>("AdminOnlyAction");
        var context = CreateHttpContext(CreateAnonymousUser());

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsUnauthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_CombinedAuthorize_DeniesAuthenticatedWithoutRole()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<CombinedAuthComponent>("AdminOnlyAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1", "User")); // Authenticated but no Admin role

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.IsForbidden.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_CombinedAuthorize_AllowsAuthenticatedWithRole()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<CombinedAuthComponent>("AdminOnlyAction");
        var context = CreateHttpContext(CreateAuthenticatedUser("1", "Admin"));

        // Act
        var result = await service.AuthorizeAsync(context, descriptor);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AuthorizeAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var descriptor = CreateDescriptor<PublicComponent>("PublicAction");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthorizeAsync(null!, descriptor));
    }

    [Fact]
    public async Task AuthorizeAsync_NullDescriptor_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var context = CreateHttpContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthorizeAsync(context, null!));
    }

    #endregion
}
