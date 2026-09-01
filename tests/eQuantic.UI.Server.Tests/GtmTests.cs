using eQuantic.UI.Gtm;
using eQuantic.UI.Server;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// UseGtm installs three things into the shell — the container snippet, the installer declaration
/// that arms the runtime's IAnalytics, and the SPA page-view listener — and refuses a wrong
/// container id at STARTUP, because a typo'd id installs a container that silently collects
/// nothing, discovered weeks later in an empty report.
/// </summary>
public class GtmTests
{
    private static List<string> HeadOf(Action<GtmOptions>? configure = null,
        string containerId = "GTM-ABC1234")
    {
        var options = new UIOptions();
        options.UseGtm(containerId, configure);
        return options.HtmlShell.HeadTags;
    }

    [Fact]
    public void InstallsTheContainer_TheInstallerDeclaration_AndThePageViewListener()
    {
        var head = HeadOf();

        var all = string.Join("\n", head);
        all.Should().Contain("googletagmanager.com/gtm.js?id='+i");
        all.Should().Contain("'GTM-ABC1234'");
        // The declaration is what flips the runtime's IAnalytics from no-op to dataLayer pushes —
        // and it must be a HEAD script, so it runs before the app bundle boots.
        all.Should().Contain("window.__EQ_ANALYTICS__={dataLayer:'dataLayer'}");
        // The navigations a tag manager cannot see: the router's own event becomes page_view,
        // under GA4's own field names so existing tags pick them up unchanged.
        all.Should().Contain("eq:navigate");
        all.Should().Contain("page_path");
    }

    /// <summary>Renaming the dataLayer reaches all three scripts — half-renamed is broken in the
    /// exact way that looks fine locally and collects nothing in production.</summary>
    [Fact]
    public void ARenamedDataLayerReachesEveryScript()
    {
        var head = HeadOf(gtm => gtm.DataLayerName = "eqData");

        var all = string.Join("\n", head);
        all.Should().Contain("{dataLayer:'eqData'}");
        all.Should().Contain("'script','eqData'");
        all.Should().Contain("window['eqData'].push");
        all.Should().NotContain("'script','dataLayer'");
    }

    [Fact]
    public void PageViewsCanBeTurnedOff_ForContainersThatTrackHistoryThemselves()
    {
        var head = HeadOf(gtm => gtm.TrackSpaPageViews = false);

        string.Join("\n", head).Should().NotContain("eq:navigate");
    }

    [Fact]
    public void AnEnvironmentPairRidesTheContainerUrl()
    {
        var head = HeadOf(gtm =>
        {
            gtm.EnvironmentAuth = "abc123";
            gtm.EnvironmentPreview = "env-5";
        });

        var all = string.Join("\n", head);
        all.Should().Contain("gtm_auth=abc123");
        all.Should().Contain("gtm_preview=env-5");
    }

    /// <summary>The id is validated where the developer is LOOKING — at startup, not in next
    /// month's empty report.</summary>
    [Theory]
    [InlineData("GTM-")]
    [InlineData("G-ABC1234")]
    [InlineData("gtm-abc1234")]
    [InlineData("UA-123456-1")]
    public void AWrongContainerIdRefusesAtStartup(string wrong)
    {
        var options = new UIOptions();

        var act = () => options.UseGtm(wrong);

        act.Should().Throw<ArgumentException>().WithMessage("*container id*");
    }

    [Fact]
    public void WithoutUseGtm_TheShellCarriesNoAnalytics()
    {
        new UIOptions().HtmlShell.HeadTags.Should().BeEmpty();
    }

    /// <summary>The agency-plus-client case: two containers, ONE dataLayer, one declaration, one
    /// page-view listener. Per-page containers are not a thing this supports — the container loads
    /// into the document and never unloads, and per-route variation is what its own triggers do.</summary>
    [Fact]
    public void ASecondContainerAddsOnlyItsSnippet()
    {
        var options = new UIOptions();
        options.UseGtm("GTM-CLIENT1").UseGtm("GTM-AGENCY1");
        var all = string.Join("\n", options.HtmlShell.HeadTags);

        all.Should().Contain("'GTM-CLIENT1'").And.Contain("'GTM-AGENCY1'");
        CountOf(all, "__EQ_ANALYTICS__").Should().Be(1, "one declaration arms the runtime once");
        CountOf(all, "eq:navigate").Should().Be(1, "one listener, or every navigation counts twice");
    }

    [Fact]
    public void TwoContainersOnDifferentDataLayersRefuse()
    {
        var options = new UIOptions();
        options.UseGtm("GTM-CLIENT1");

        var act = () => options.UseGtm("GTM-AGENCY1", gtm => gtm.WithDataLayerName("other"));

        act.Should().Throw<ArgumentException>().WithMessage("*same name*");
    }

    /// <summary>The fluent voice, end to end — the way a Program.cs reads.</summary>
    [Fact]
    public void TheFluentSpellingConfiguresEverything()
    {
        var head = HeadOf(gtm => gtm
            .WithDataLayerName("eqData")
            .WithoutSpaPageViews()
            .WithEnvironment("abc", "env-9"));

        var all = string.Join("\n", head);
        all.Should().Contain("'script','eqData'");
        all.Should().NotContain("eq:navigate");
        all.Should().Contain("gtm_auth=abc").And.Contain("gtm_preview=env-9");
    }

    private static int CountOf(string text, string needle)
    {
        var count = 0;
        var at = 0;
        while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { count++; at += needle.Length; }
        return count;
    }

    [Fact]
    public void WithConsent_LoadsTheContainerOnlyOnAGrantedAnswer()
    {
        var all = string.Join("\n", HeadOf(gtm => gtm.WithConsent()));

        // Google Consent Mode defaults to DENIED before any container exists to read them.
        all.Should().Contain("gtag('consent','default',{ad_storage:'denied'");
        all.Should().Contain("analytics_storage:'denied'");
        // The container is fetched only when the shared cookie says granted, or when the browser
        // announces a grant — the event IConsent's web realization dispatches.
        all.Should().Contain("eq-consent=([^;]*)");
        all.Should().Contain("m[1]==='granted'");
        all.Should().Contain("addEventListener('eq:consent'");
        all.Should().Contain("googletagmanager.com/gtm.js?id='+i");
        // And the installer declaration still arms IAnalytics: pushes land in the dataLayer array,
        // which the container replays when it finally loads.
        all.Should().Contain("window.__EQ_ANALYTICS__={dataLayer:'dataLayer'}");
    }

    [Fact]
    public void WithoutConsent_TheContainerLoadsUnconditionally_AsBefore()
    {
        var all = string.Join("\n", HeadOf());
        all.Should().NotContain("eq-consent");
        all.Should().NotContain("'consent','default'");
    }

    /// <summary>The cookie name is spelled in two packages on purpose (Gtm stays dependency-free);
    /// this is what keeps the two spellings one.</summary>
    [Fact]
    public void TheConsentCookie_IsTheOneTheServerReads()
    {
        var all = string.Join("\n", HeadOf(gtm => gtm.WithConsent()));
        all.Should().Contain(ConsentCookie.Name + "=([^;]*)");
    }
}
