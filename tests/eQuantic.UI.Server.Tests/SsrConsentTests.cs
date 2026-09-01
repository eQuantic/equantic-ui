using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The server's answer to "did this visitor consent?" is the REQUEST'S: the browser stores the
/// reply in eq-consent, and reading it here is what makes the first paint agree with the browser —
/// no banner flashing for a visitor who answered last week.
/// </summary>
public class SsrConsentTests
{
    private static SsrConsent WithCookie(string? cookie)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null) context.Request.Headers.Cookie = cookie;
        return new SsrConsent(new HttpContextAccessor { HttpContext = context });
    }

    [Theory]
    [InlineData("eq-consent=granted", ConsentState.Granted)]
    [InlineData("eq-consent=denied", ConsentState.Denied)]
    [InlineData("eq-theme=dark; eq-consent=granted", ConsentState.Granted)]
    public void ReadsTheAnswerTheRequestCarries(string cookie, ConsentState expected) =>
        WithCookie(cookie).State.Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("eq-theme=dark")]
    [InlineData("eq-consent=yes-please")]
    public void AnythingElseIsUnanswered_NeverConsent(string? cookie) =>
        WithCookie(cookie).State.Should().Be(ConsentState.Unknown);

    [Fact]
    public void WithNoRequestAtAll_ItIsUnknown() =>
        new SsrConsent().State.Should().Be(ConsentState.Unknown);

    [Fact]
    public void MutationsAreNoOps_TheReplyBelongsToTheNextRequest()
    {
        var consent = WithCookie(null);
        consent.Grant();
        consent.Deny();
        consent.State.Should().Be(ConsentState.Unknown);
    }
}
