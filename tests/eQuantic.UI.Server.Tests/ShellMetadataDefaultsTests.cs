using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Server;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// App-wide metadata defaults, and a page's right to override them.
/// <para>
/// The shell's description used to be raw head HTML. Raw HTML shares no key with anything, so an
/// app that set one globally and a page that set its own shipped TWO
/// <c>&lt;meta name="description"&gt;</c> — and no page could win, because there was nothing to
/// win against. The only workaround was to leave the global empty, which made it useless for the
/// one thing a global is for.
/// </para>
/// </summary>
public class ShellMetadataDefaultsTests
{
    private static MetadataCollection Merge(HtmlShellOptions shell, Action<SeoBuilder>? page = null)
    {
        // What the render path does: the shell's defaults seed the collection, the page's own
        // AddOrUpdate over it.
        var metadata = new MetadataCollection { Title = shell.Title };
        foreach (var tag in shell.DefaultMetadata.Tags) metadata.AddOrUpdate(tag);
        page?.Invoke(new SeoBuilder(metadata));
        return metadata;
    }

    [Fact]
    public void AGlobalDescription_ReachesAPageThatSaysNothing()
    {
        var shell = new HtmlShellOptions().AddDescription("the app's own words");

        Merge(shell).Tags.Should().ContainSingle(t => t.Key == "name:description");
    }

    /// <summary>The one that was broken: a page's description REPLACES the app's, rather than
    /// being emitted beside it.</summary>
    [Fact]
    public void APagesDescription_ReplacesTheGlobalOne()
    {
        var shell = new HtmlShellOptions().AddDescription("the app's own words");

        var metadata = Merge(shell, seo => seo.Description("what THIS page is about"));

        metadata.Tags.Should().ContainSingle(t => t.Key == "name:description",
            "two descriptions is a crawler being told the page cannot make up its mind");
        metadata.RenderTags().Should().Contain("what THIS page is about")
            .And.NotContain("the app's own words");
    }

    /// <summary>A global share image is the case this generalises to — every page gets one without
    /// repeating it, and a page with a better one wins.</summary>
    [Fact]
    public void AGlobalShareImage_IsOverriddenPerPage()
    {
        var shell = new HtmlShellOptions()
            .ConfigureMetadata(seo => seo.Image("https://example.test/og-default.png", "the app"));

        var metadata = Merge(shell, seo => seo.Image("https://example.test/og-playground.png"));

        metadata.Tags.Where(t => t.Key == "property:og:image").Should().ContainSingle();
        metadata.RenderTags().Should().Contain("og-playground.png").And.NotContain("og-default.png");
    }

    /// <summary>What a page does NOT restate, it keeps — that is what makes the global a default
    /// rather than an all-or-nothing switch.</summary>
    [Fact]
    public void WhatThePageLeavesAlone_Survives()
    {
        var shell = new HtmlShellOptions()
            .ConfigureMetadata(seo => seo.Twitter("card", "summary_large_image"));

        var metadata = Merge(shell, seo => seo.Description("only a description here"));

        metadata.RenderTags().Should().Contain("summary_large_image");
    }
}
