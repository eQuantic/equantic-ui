using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// WHAT A PROGRESS BAR AND A SLIDER TELL A NATIVE BRIDGE (#243) — the words when there are any, and
/// the NUMBERS beside them.
///
/// <para>
/// `SemanticNode.Value` is a string, so until this the numbers were formatted away before any shell
/// saw them and no bridge could populate its platform's own range: Android's
/// <c>AccessibilityNodeInfo.RangeInfo</c>, or <c>AXMinValue</c>/<c>AXMaxValue</c> on macOS. The web
/// was the only target that got the real trio, because it reads the NODE rather than this snapshot.
/// </para>
///
/// <para>
/// The web twin of the words half is <c>eQuantic.UI.Web.Tests.ProgressSemanticsTests</c>; the two
/// sides are pinned separately because the web suite never reaches the bridges, which is the gap
/// #243 opens with.
/// </para>
/// </summary>
public class ProgressRangeSemanticsTests
{
    private static IReadOnlyList<SemanticNode> Describe(VisualNode node)
    {
        var host = new PhotonHost(node, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    private static SemanticNode Bar(VisualNode node) =>
        Describe(node).Single(s => s.Role == SemanticRole.ProgressIndicator);

    /// <summary>
    /// A determinate bar carries the numbers a bridge needs to place the value. Before this it
    /// carried "0.45" and nothing to read it against, so TalkBack could map the role to
    /// <c>android.widget.ProgressBar</c> and still not treat it as an indicator.
    /// <para>Mutation: stop passing `Range` in the Progress arm and this fails.</para>
    /// </summary>
    [Fact]
    public void ADeterminateBarCarriesItsNumbersAndNotJustTheString()
    {
        var bar = Bar(new ProgressBar(0.45f) { Label = "Uploading" });

        bar.Range.Should().NotBeNull();
        bar.Range!.Value.Min.Should().Be(0);
        bar.Range.Value.Max.Should().Be(1);
        bar.Range.Value.Now.Should().BeApproximately(0.45f, 0.001f);
    }

    /// <summary>
    /// AND AN INDETERMINATE ONE CARRIES NONE — a state, not a missing number. A bridge that saw a
    /// range here would report a position on a bar that has none, which is worse than reporting
    /// nothing: Android's own indeterminate ProgressBar reports no position either.
    /// </summary>
    [Fact]
    public void AnIndeterminateBarCarriesNoNumbers()
    {
        Bar(new ProgressBar { Label = "Syncing" }).Range.Should().BeNull(
            "indeterminate is something the role reports, not a number that went missing");
    }

    /// <summary>
    /// THE WORDS SURVIVE THE MISSING NUMBER, which #243 calls the real loss of the two. They used
    /// to ride inside the `RangeValue`, so a bar with no value had no words: `ValueText` was
    /// silently discarded by `ProgressBar.Build`'s indeterminate branch.
    /// <para>Mutation: drop `ValueText = ValueText` from that branch and this fails alone.</para>
    /// </summary>
    [Fact]
    public void AnIndeterminateBarStillAnnouncesItsWords()
    {
        Bar(new ProgressBar { Label = "Syncing", ValueText = "Estimating time remaining" })
            .Value.Should().Be("Estimating time remaining",
                "there is no number to fall back on, which is exactly why the words matter here");
    }

    /// <summary>The words REPLACE the number when both are there — the same rule
    /// <c>aria-valuetext</c> states on the web, so the two targets announce one thing.</summary>
    [Fact]
    public void WordsReplaceTheNumberAndTheNumbersTravelAnyway()
    {
        var bar = Bar(new ProgressBar(0.45f) { Label = "Uploading", ValueText = "3 of 7 files" });

        bar.Value.Should().Be("3 of 7 files");
        bar.Range.Should().NotBeNull("a bridge still needs the bounds to place what it is given");
    }

    /// <summary>A bar with neither says neither — the announcement is the name alone.</summary>
    [Fact]
    public void ABarWithNoWordsAndNoNumberAnnouncesItsNameAlone()
    {
        var bar = Bar(new ProgressBar { Label = "Syncing" });

        bar.Label.Should().Be("Syncing");
        bar.Value.Should().BeNull();
    }

    /// <summary>
    /// A SLIDER carries its numbers too, and under the same condition as its words: a node the
    /// realizer does not announce as a slider is not announced as having a range either.
    /// </summary>
    [Fact]
    public void ASliderCarriesItsNumbers()
    {
        var slider = Describe(new Slider(0.5f) { Label = "Volume", Min = 0, Max = 2 })
            .Single(s => s.Role == SemanticRole.Slider);

        slider.Range.Should().NotBeNull();
        slider.Range!.Value.Min.Should().Be(0);
        slider.Range.Value.Max.Should().Be(2);
    }
}
