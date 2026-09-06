using eQuantic.UI.Native.Hosting;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What <c>builder.Bundle.UrlScheme(…)</c> stores. The rule is one file the builder and the
/// generator both compile (<c>BundleFactRule</c>), so what is pinned here is what the manifest
/// carries too.
/// </summary>
public class BundleBuilderTests
{
    /// <summary>
    /// Schemes compare without regard to case everywhere they are matched (RFC 3986), so two casings
    /// of one scheme are ONE manifest entry — and the one spelling is the lower-case canonical form,
    /// which is also the spelling <c>OpenUrlPolicy</c> stores its own in.
    /// </summary>
    [Fact]
    public void ASchemeIsStoredInItsCanonicalSpelling_SoTwoCasingsAreOneEntry()
    {
        var bundle = new PhotonBundleBuilder();

        bundle.UrlScheme("Acme").UrlScheme("acme").UrlScheme(" ACME ");

        bundle.UrlSchemes.Should().Equal("acme");
    }

    [Fact]
    public void DistinctSchemesAccumulate_InTheOrderTheyWereSaid()
    {
        var bundle = new PhotonBundleBuilder();

        bundle.UrlScheme("acme").UrlScheme("acme-beta");

        bundle.UrlSchemes.Should().Equal("acme", "acme-beta");
    }

    /// <summary>
    /// `file` is refused HERE, at the line that declared it, and not later: no app answers to the
    /// system's own scheme however the manifest is written, and the policy that inherits an app's
    /// schemes at Build() would otherwise be the one to throw, about OpenUrl, for a line about the
    /// bundle.
    /// </summary>
    [Theory]
    [InlineData("acme://")]
    [InlineData("1acme")]
    [InlineData("ac me")]
    [InlineData("")]
    [InlineData("file")]
    [InlineData("FILE")]
    public void WhatIsNotASchemeNameThrowsAtItsOwnLine(string scheme)
    {
        var bundle = new PhotonBundleBuilder();

        var declaring = () => bundle.UrlScheme(scheme);

        declaring.Should().Throw<ArgumentException>();
        bundle.UrlSchemes.Should().BeEmpty("nothing half-accepted reaches the manifest");
    }
}
