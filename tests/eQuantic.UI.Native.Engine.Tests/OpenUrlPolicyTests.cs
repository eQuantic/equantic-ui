using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The one rule every <see cref="IWorkspace"/> realization applies before a URL reaches the
/// platform. The platform half — NSWorkspace on a Mac — is proved live, and PR #86 says why it has
/// no unit test; the DECISION is pure and lives in Primitives, so it is pinned here, where a Mac
/// and a phone cannot come to disagree about the same URL.
/// </summary>
public class OpenUrlPolicyTests
{
    [Theory]
    [InlineData("https://equantic.tech/")]
    [InlineData("http://localhost:5000/health")]
    [InlineData("HTTPS://EQUANTIC.TECH/")]
    public void TheWebIsOpenByDefault(string url) =>
        OpenUrlPolicy.Web.Allows(new Uri(url)).Should().BeTrue();

    /// <summary>
    /// The consumer's exact list: `open` obeys any scheme registered on the machine, a `file://`
    /// opens a folder or launches what it names, and some schemes execute. None of them may be
    /// reached by a URL that arrived in content, so none of them is open by default — including the
    /// app's own `acme://`, which the BUILDER adds from the bundle declaration and this policy alone
    /// does not know about.
    /// </summary>
    [Theory]
    [InlineData("file:///Applications")]
    [InlineData("file:///Applications/Utilities/Terminal.app")]
    [InlineData("mailto:support@acme.example")]
    [InlineData("tel:+351000000000")]
    [InlineData("sms:+351000000000")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ssh://root@host")]
    [InlineData("acme://activate?key=abc")]
    [InlineData("x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles")]
    public void EverythingElseIsRefusedByDefault(string url) =>
        OpenUrlPolicy.Web.Allows(new Uri(url)).Should().BeFalse();

    [Fact]
    public void AllowingWidens_AndLeavesTheOriginalAlone()
    {
        var mail = OpenUrlPolicy.Web.Allowing(Uri.UriSchemeMailto);

        mail.Allows(new Uri("mailto:support@acme.example")).Should().BeTrue();
        mail.Allows(new Uri("https://acme.example")).Should().BeTrue("widening never narrows");
        mail.Allows(new Uri("tel:+1")).Should().BeFalse();
        // The default is a shared instance; a policy that mutated it would open mailto for every
        // app in the process that did not ask.
        OpenUrlPolicy.Web.Allows(new Uri("mailto:support@acme.example")).Should().BeFalse();
    }

    [Fact]
    public void ASchemeIsAName_ComparedWithoutRegardToCaseOrWhitespace()
    {
        var policy = OpenUrlPolicy.Web.Allowing("  MailTo ");

        policy.Schemes.Should().Contain("mailto");
        policy.Allows(new Uri("MAILTO:a@b.example")).Should().BeTrue("Uri lowercases the scheme, and so does the policy");
    }

    [Fact]
    public void AllowingWhatIsAlreadyOpen_IsTheSamePolicy()
    {
        OpenUrlPolicy.Web.Allowing("HTTPS").Should().BeSameAs(OpenUrlPolicy.Web);
    }

    [Fact]
    public void SeveralAtOnce_IsOneAtATime()
    {
        var several = OpenUrlPolicy.Web.Allowing(["acme", "slack"]);

        several.Schemes.Should().BeEquivalentTo(["http", "https", "acme", "slack"]);
        several.Allows(new Uri("slack://open")).Should().BeTrue();
    }

    /// <summary>
    /// Not a scheme is the CALLER's mistake, so it throws — the contract's own line between "the
    /// system declined" and "this could never have been asked". The trailing colon is the way
    /// everyone writes it wrong first.
    /// </summary>
    [Theory]
    [InlineData("mailto:")]
    [InlineData("mailto://")]
    [InlineData("1acme")]
    [InlineData("acme scheme")]
    [InlineData("")]
    [InlineData("   ")]
    public void WhatIsNotASchemeNameIsTheCallersMistake(string scheme)
    {
        var allowing = () => OpenUrlPolicy.Web.Allowing(scheme);
        allowing.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// The one scheme that cannot be opted in, and the reason is a POINTER: a file has typed doors
    /// that check the path exists and never launch a folder, and the refusal names them.
    /// </summary>
    [Theory]
    [InlineData("file")]
    [InlineData("FILE")]
    [InlineData(" File ")]
    public void FileCannotBeOptedIn_BecauseAFileHasTypedDoors(string scheme)
    {
        var allowing = () => OpenUrlPolicy.Web.Allowing(scheme);
        allowing.Should().Throw<ArgumentException>().WithMessage("*OpenFile*Reveal*");
    }

    /// <summary>
    /// The sentence a realization logs. It has to name the scheme (so the developer knows WHICH
    /// link) and the one line that opens it (so they do not go looking at the operating system),
    /// and it is written once so every platform says the same thing.
    /// </summary>
    [Fact]
    public void ARefusalNamesTheSchemeAndTheOneLineFix()
    {
        var refusal = OpenUrlPolicy.Web.Refusal(new Uri("mailto:support@acme.example"));

        refusal.Should().Contain("\"mailto\"")
            .And.Contain("builder.Workspace.Opens(\"mailto\")")
            .And.Contain("http, https", "it says what IS open, so the developer sees the gap");
    }

    [Fact]
    public void AnAllowedUrlHasNoRefusal()
    {
        OpenUrlPolicy.Web.Refusal(new Uri("https://equantic.tech/")).Should().BeNull();
    }

    /// <summary>
    /// A relative URL is refused by the policy too — it has no scheme to allow — but it is not the
    /// policy's decision: a realization throws for it before asking, because it is the caller's
    /// mistake. The policy still answers sensibly, so a realization that asks first is not misled.
    /// </summary>
    [Fact]
    public void ARelativeUrlIsRefused_AndSaysItIsRelative()
    {
        var relative = new Uri("/settings/privacy", UriKind.Relative);

        OpenUrlPolicy.Web.Allows(relative).Should().BeFalse();
        OpenUrlPolicy.Web.Refusal(relative).Should().Contain("relative");
    }
}
