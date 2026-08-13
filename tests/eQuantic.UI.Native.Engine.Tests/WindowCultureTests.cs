using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// TRACK L, M3 — two cultures in a WINDOW, from the same component classes.
/// <para>
/// The web half of this story was proven by a page: one request, one culture, SSR and hydration
/// agreeing. A window has no request. The platform decides the locale, someone has to copy it onto
/// .NET's statics, and the tree has to REBUILD for the switch to reach pixels — three joints the
/// web never has, and the reason this milestone was written down separately.
/// </para>
/// <para>
/// The strings here are the SDK's OWN (<c>SdkStrings</c>, resx-backed with pt-BR and es
/// satellites), for one reason worth stating: an app that authors no resx at all still renders a
/// localized window, because the framework's chrome carries its own translations. If these read
/// English after a switch, so does every screen reader announcement in every app.
/// </para>
/// </summary>
public class WindowCultureTests
{
    /// <summary>
    /// One component, no culture knowledge. It asks the vocabulary for its strings and never
    /// learns which culture answered — the same class the SSR half renders.
    /// </summary>
    private sealed class SettingsPage : StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new Text(SdkStrings.Find, TypeRole.Heading, context.Theme.TextPrimary));
            column.Add(new Pressable(new Text(SdkStrings.Dismiss, TypeRole.Label, context.Theme.TextPrimary),
                () => { }));
            column.Add(new Text(SdkStrings.Spreadsheet, TypeRole.BodyM, context.Theme.TextMuted));
            return column;
        }
    }

    /// <summary>The window as the runner builds it: a host, and a controller whose repaint is the
    /// host's own invalidate — the wiring `PhotonWindow` performs once a window exists.</summary>
    private static (PhotonHost Host, PhotonCultureController Culture) OpenWindow()
    {
        var host = new PhotonHost(new SettingsPage(), PhotonTheme.Instance, ThemeMode.Light, 420, 320);
        var culture = new PhotonCultureController();
        culture.Attach(host.Invalidate);
        host.RenderFrame(new DisplayListBuilder());
        return (host, culture);
    }

    private static IReadOnlyList<string> TextOf(PhotonHost host) =>
        [.. host.Semantics().Select(node => node.Label).Where(label => !string.IsNullOrEmpty(label))!];

    /// <summary>
    /// The milestone itself: the SAME tree, two cultures, and the second render says the second
    /// language. Nothing in <see cref="SettingsPage"/> changed between the two frames.
    /// </summary>
    [Fact]
    public void TheSameTreeRendersTwoCultures()
    {
        var previousUi = CultureInfo.DefaultThreadCurrentUICulture;
        var previousFormat = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var (host, culture) = OpenWindow();

            culture.Apply(CultureInfo.GetCultureInfo("en"));
            host.RenderFrame(new DisplayListBuilder());
            TextOf(host).Should().Contain("Find");

            culture.Apply(CultureInfo.GetCultureInfo("pt-BR"));
            host.RenderFrame(new DisplayListBuilder());

            var translated = TextOf(host);
            translated.Should().Contain("Localizar", "the window rebuilt in the culture that was applied");
            translated.Should().NotContain("Find", "a switch replaces the text, it does not add to it");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi ?? CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = previousFormat ?? CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// A third culture, because two can agree by accident: es resolves from its own satellite, and
    /// a culture with no satellite at all falls back to the neutral English rather than throwing.
    /// </summary>
    [Fact]
    public void AThirdCultureAnswers_AndAnUntranslatedOneFallsBack()
    {
        var previousUi = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            var (host, culture) = OpenWindow();

            culture.Apply(CultureInfo.GetCultureInfo("es"));
            host.RenderFrame(new DisplayListBuilder());
            TextOf(host).Should().Contain("Buscar");

            // Japanese ships no satellite: the neutral answer is the honest one.
            culture.Apply(CultureInfo.GetCultureInfo("ja"));
            host.RenderFrame(new DisplayListBuilder());
            TextOf(host).Should().Contain("Find");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previousUi;
            CultureInfo.CurrentUICulture = previousUi ?? CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// A switch REPAINTS. The controller's repaint is the host's invalidate, so applying a culture
    /// asks for the next frame the way <c>SetState</c> does — without it the process would speak
    /// the new language while the window kept showing the old one until something else moved.
    /// </summary>
    [Fact]
    public void ApplyingACultureAsksForTheNextFrame()
    {
        var previousUi = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            var (host, culture) = OpenWindow();
            host.RenderFrame(new DisplayListBuilder());
            host.NeedsRender.Should().BeFalse("a settled window presents nothing");

            culture.Apply(CultureInfo.GetCultureInfo("pt-BR"));

            host.NeedsRender.Should().BeTrue("the culture hand repaints, like the theme hand does");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previousUi;
            CultureInfo.CurrentUICulture = previousUi ?? CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// The D13 PAIR: resources and formats are two decisions. A reader can want a Portuguese
    /// interface with the formats of where they live, and the controller carries both — the same
    /// pair `CurrentUICulture` and `CurrentCulture` have modelled in .NET forever.
    /// </summary>
    [Fact]
    public void TheUiCultureAndTheFormatCultureAreSeparate()
    {
        var previousUi = CultureInfo.DefaultThreadCurrentUICulture;
        var previousFormat = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var (host, culture) = OpenWindow();

            culture.Apply(CultureInfo.GetCultureInfo("pt-BR"), CultureInfo.GetCultureInfo("en-US"));
            host.RenderFrame(new DisplayListBuilder());

            TextOf(host).Should().Contain("Localizar", "the interface speaks the UI culture");
            1234.5m.ToString("C2", CultureInfo.CurrentCulture)
                .Should().StartWith("$", "money speaks the FORMAT culture, which was set apart");
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi ?? CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = previousFormat ?? CultureInfo.InvariantCulture;
        }
    }
}
