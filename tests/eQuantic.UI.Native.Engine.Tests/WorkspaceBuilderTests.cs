using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What <c>Program.cs</c> declares reaches the container as ONE <see cref="OpenUrlPolicy"/>: the
/// web, the app's own schemes — said once, for the manifest, and read from the same declaration —
/// and what <c>builder.Workspace</c> opened. The realization that consults it is platform code; the
/// composition is not, and this is where it is pinned.
/// </summary>
public class WorkspaceBuilderTests
{
    private static OpenUrlPolicy PolicyOf(PhotonApplication app) =>
        app.Services.GetRequiredService<OpenUrlPolicy>();

    [Fact]
    public void ByDefaultTheAppOpensTheWeb_AndItsOwnSchemes()
    {
        var builder = PhotonApplication.CreateBuilder();
        builder.Bundle.UrlScheme("acme");

        var policy = PolicyOf(builder.Build());

        policy.Allows(new Uri("https://acme.example/pricing")).Should().BeTrue();
        policy.Allows(new Uri("acme://activate?key=abc")).Should().BeTrue(
            "the SDK already knows the app's own scheme from the bundle declaration — nobody says it twice");
        policy.Allows(new Uri("mailto:support@acme.example")).Should().BeFalse("nothing opened it");
        policy.Allows(new Uri("file:///Applications")).Should().BeFalse();
    }

    [Fact]
    public void WhatProgramCsOpensIsOpen()
    {
        var builder = PhotonApplication.CreateBuilder();
        builder.Workspace.OpensMail().OpensPhone().OpensMessages().Opens("x-apple.systempreferences");

        var policy = PolicyOf(builder.Build());

        policy.Allows(new Uri("mailto:support@acme.example")).Should().BeTrue();
        policy.Allows(new Uri("tel:+351000000000")).Should().BeTrue();
        policy.Allows(new Uri("sms:+351000000000")).Should().BeTrue();
        policy.Allows(new Uri("x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles"))
            .Should().BeTrue("the settings pane a disk tool sends a person to for Full Disk Access");
        policy.Allows(new Uri("slack://open")).Should().BeFalse("what was not opened stays closed");
    }

    [Fact]
    public void TheBuilderSaysWhatItDeclared()
    {
        var builder = PhotonApplication.CreateBuilder();

        builder.Workspace.OpensMail();

        builder.Workspace.Declared.Schemes.Should().BeEquivalentTo(["http", "https", "mailto"]);
    }

    /// <summary>
    /// TryAdd, like every capability: an app that registered its own policy before Build has
    /// decided — the builder's composition, own schemes included, steps aside for it.
    /// </summary>
    [Fact]
    public void AnAppThatRegisteredItsOwnPolicyHasDecided()
    {
        var builder = PhotonApplication.CreateBuilder();
        var own = OpenUrlPolicy.Web.Allowing("zzz");
        builder.Services.AddSingleton(own);
        builder.Workspace.OpensMail();
        builder.Bundle.UrlScheme("acme");

        var policy = PolicyOf(builder.Build());

        policy.Should().BeSameAs(own);
        policy.Allows(new Uri("mailto:x@acme.example")).Should().BeFalse();
        policy.Allows(new Uri("acme://x")).Should().BeFalse();
    }

    /// <summary>
    /// A scheme that is not one fails at ITS line in Program.cs, on either builder — not at the
    /// first click, and not as a CFBundleURLTypes entry nothing ever matches.
    /// </summary>
    [Fact]
    public void ASchemeThatIsNotOne_FailsAtItsOwnLine()
    {
        var builder = PhotonApplication.CreateBuilder();

        var bundle = () => builder.Bundle.UrlScheme("acme://");
        var workspace = () => builder.Workspace.Opens("mailto:");

        bundle.Should().Throw<ArgumentException>().WithMessage("*\"acme://\" cannot be this app's URL scheme*");
        workspace.Should().Throw<ArgumentException>().WithMessage("*\"mailto:\" is not a URL scheme*");
    }

    [Fact]
    public void FileIsRefusedOnTheBuilderToo_PointingAtTheTypedDoors()
    {
        var builder = PhotonApplication.CreateBuilder();

        var opens = () => builder.Workspace.Opens("file");

        opens.Should().Throw<ArgumentException>().WithMessage("*OpenFile*Reveal*");
    }
}
