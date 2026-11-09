using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>A scheme under which a request is signed in as the user its <c>X-User</c> header names, by
/// the identifier a sign-in gives, as a cookie or a token would make it; a request without it is a
/// visitor.</summary>
internal sealed class HeaderUser(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Name = "HeaderUser";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Request.Headers["X-User"].ToString() is { Length: > 0 } user
            ? AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], Name)), Name))
            : AuthenticateResult.NoResult());
}
