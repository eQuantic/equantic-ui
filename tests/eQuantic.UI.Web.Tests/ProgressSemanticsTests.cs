using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A ProgressBar says WHAT IT IS FOR and HOW FAR ALONG (spec B14).
/// <para>
/// It lowered to a bare Row or Box — an unlabelled pair of divs to a screen reader, and nothing at
/// all on Photon — because the vocabulary had no node that could carry a progress role. The audit
/// filed it as the same class of gap as the slider's missing value, and it is: a component that
/// paints a fact and never states it.
/// </para>
/// <para>
/// The rule this pins is the INVERSE of the slider's, which is the interesting part. There, a
/// missing value means the node is not a slider and the realizer withholds the role. Here, a
/// missing value is INDETERMINATE — a state the role exists to report — so the role stays and only
/// the number goes. Two rules that look alike and must not be merged.
/// </para>
/// </summary>
public class ProgressSemanticsTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Lower(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static HtmlNode Host(VisualNode node) =>
        Walk(Lower(node)).First(n => n.Attributes.GetValueOrDefault("role") == "progressbar");

    [Fact]
    public void ADeterminateBarStatesHowFarAlongItIs()
    {
        var host = Host(new ProgressBar(0.45f) { Label = "Uploading" });

        host.Attributes["aria-valuenow"].Should().Be("0.45");
        host.Attributes["aria-valuemin"].Should().Be("0");
        host.Attributes["aria-valuemax"].Should().Be("1");
        host.Attributes["aria-label"].Should().Be("Uploading");
        host.Attributes.Should().NotContainKey("aria-valuetext",
            "a bare ratio speaks for itself — a reader renders it as a percentage on its own");
    }

    /// <summary>
    /// ARIA's own rule, and the honest one: the bar is saying that something is happening, which is
    /// all it knows. Dropping the ROLE as well would leave a screen-reader user with no clue that
    /// anything is in flight.
    /// </summary>
    [Fact]
    public void AnIndeterminateBarKeepsTheRoleAndDropsOnlyTheNumber()
    {
        var host = Host(new ProgressBar { Label = "Syncing" });

        host.Attributes["role"].Should().Be("progressbar");
        host.Attributes["aria-label"].Should().Be("Syncing");
        host.Attributes.Should().NotContainKey("aria-valuenow");
        host.Attributes.Should().NotContainKey("aria-valuemin");
        host.Attributes.Should().NotContainKey("aria-valuemax");
    }

    /// <summary>
    /// The announcement is the value the BAR IS DRAWN FROM. The flex weights come from a value
    /// clamped to 0..1, so announcing the caller's raw number would put aria-valuenow outside the
    /// aria-valuemax beside it and make the pixels and the words describe different controls — the
    /// same defect the slider had, caught here before it shipped rather than after.
    /// </summary>
    [Theory]
    [InlineData(1.8f, "1")]
    [InlineData(-0.5f, "0")]
    [InlineData(0.25f, "0.25")]
    public void TheAnnouncedValueIsTheOneTheBarIsDrawnFrom(float value, string announced)
    {
        Host(new ProgressBar(value)).Attributes["aria-valuenow"].Should().Be(announced);
    }

    /// <summary>"3 of 7 files" — the words are the app's, because only the app knows what its
    /// ratio counts.</summary>
    [Fact]
    public void WordsReplaceTheRatioWhenTheCallerGivesThem()
    {
        var host = Host(new ProgressBar(0.43f) { Label = "Copying", ValueText = "3 of 7 files" });

        host.Attributes["aria-valuetext"].Should().Be("3 of 7 files");
        host.Attributes["aria-valuenow"].Should().Be("0.43", "the number stays for anything that computes with it");
    }

    /// <summary>
    /// Reachable from the AUTHORING surface. Every configuration on this component is init-only, so
    /// a factory that did not take them put them out of reach of every screen written the sanctioned
    /// way — the gap the Slider's factory had, fixed here at the same time rather than left to be
    /// found again.
    /// </summary>
    [Fact]
    public void TheFactoryCanExpressWhatTheComponentHolds()
    {
        var host = Host(Components.UI.ProgressBar(0.6f, label: "Restoring", valueText: "60%", prominent: true));

        host.Attributes["aria-valuenow"].Should().Be("0.6");
        host.Attributes["aria-label"].Should().Be("Restoring");
        host.Attributes["aria-valuetext"].Should().Be("60%");
    }

    /// <summary>
    /// A progress bar is READ, never moved. Emitting a tab stop or a key handler would put it in the
    /// Tab order and promise a gesture that does nothing — which is exactly what calling it a slider
    /// on the native side would have done.
    /// </summary>
    [Fact]
    public void TheBarIsNotAControl()
    {
        foreach (var bar in new VisualNode[] { new ProgressBar(0.3f), new ProgressBar() })
        {
            var host = Host(bar);

            host.Attributes.Should().NotContainKey("tabindex");
            host.Events.Should().BeEmpty("nothing here is operable");
        }
    }

    /// <summary>
    /// THE RULE, not the instance: a progress role reaches the markup for every ProgressBar, and it
    /// carries the number whenever the bar HAS one. Stated as a sweep so a second component that
    /// grows a progress role cannot state it half-way.
    /// </summary>
    [Fact]
    public void EveryDeterminateBarReachesTheMarkupWithItsValue()
    {
        (string Name, VisualNode Node, bool Determinate)[] cases =
        [
            ("plain", new ProgressBar(0.45f), true),
            ("labelled", new ProgressBar(0.45f) { Label = "Uploading" }, true),
            ("prominent", new ProgressBar(0.45f) { Prominent = true }, true),
            ("full", new ProgressBar(1f), true),
            ("empty", new ProgressBar(0f), true),
            ("factory", Components.UI.ProgressBar(0.2f, label: "Indexing"), true),
            ("indeterminate", new ProgressBar(), false),
            ("indeterminate-labelled", new ProgressBar { Label = "Syncing" }, false),
        ];

        var hosts = cases.ToDictionary(
            one => one.Name,
            one => Walk(Lower(one.Node))
                .Where(node => node.Attributes.GetValueOrDefault("role") == "progressbar")
                .ToArray());

        string.Join(", ", hosts.Where(pair => pair.Value.Length != 1).Select(pair => pair.Key))
            .Should().BeEmpty("every ProgressBar states the progress role exactly once");

        var naked = cases
            .Where(one => one.Determinate && !hosts[one.Name][0].Attributes.ContainsKey("aria-valuenow"))
            .Select(one => one.Name);
        string.Join(", ", naked).Should().BeEmpty(
            "a determinate bar knows how far along it is, so the markup says so");

        var overstated = cases
            .Where(one => !one.Determinate && hosts[one.Name][0].Attributes.ContainsKey("aria-valuenow"))
            .Select(one => one.Name);
        string.Join(", ", overstated).Should().BeEmpty(
            "an indeterminate bar invents no number — omitting aria-valuenow IS the announcement");
    }
}
