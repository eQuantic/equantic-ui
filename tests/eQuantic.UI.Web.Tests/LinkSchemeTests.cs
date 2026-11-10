using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// #1286: the destinations an href may carry, read as a browser's URL parser reads a scheme. The same
/// table runs against the TypeScript twin (<c>utils/link-scheme.spec.ts</c>), and the parity case
/// <c>links-with-schemes</c> holds the two realizers to the same markup.
/// </summary>
public class LinkSchemeTests
{
    [Theory]
    [InlineData("/pricing")]
    [InlineData("docs/next?page=2")]
    [InlineData("?page=2")]
    [InlineData("#top")]
    [InlineData("//cdn.example.com/x")]
    [InlineData("https://example.com")]
    [InlineData("HTTPS://example.com")]
    [InlineData("http://example.com")]
    [InlineData("mailto:team@example.com")]
    [InlineData("tel:+351210000000")]
    [InlineData("")]
    [InlineData("a/b:c")]
    [InlineData("javascript&colon;alert(1)")]
    public void ADestinationWithNoSchemeOrAnAllowedOne_IsWrittenAsItIs(string destination) =>
        RenderContext.ResolveDestination(destination).Should().Be(destination);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("\u0001javascript:alert(1)")]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("java\nscript:alert(1)")]
    [InlineData("java\rscript:alert(1)")]
    [InlineData("vbscript:msgbox")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    public void ADestinationWithAnyOtherScheme_IsNoHref(string destination) =>
        RenderContext.ResolveDestination(destination).Should().BeNull();
}
