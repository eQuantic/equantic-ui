using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A tab strip and a radio group are CONTAINERS on Photon, read and then walked into, the way the
/// web's <c>tablist</c> of <c>tab</c>s and <c>radiogroup</c> of <c>radio</c>s are (#500).
/// <para>
/// Both are an <see cref="Adjustable"/>, one keyboard stop whose arrows move the pick, and the
/// semantics walk announced every Adjustable as a slider. Measured on #338's branch, a
/// <see cref="Tabs"/> and a <see cref="RadioGroup"/> each reached the bridges as one
/// <c>Slider label='' value=''</c> stop, which macOS calls a slider and Android a SeekBar, and the tabs
/// and radios inside, which carry the names and say which one is picked, were never read. Each
/// test here fails against that walk.
/// </para>
/// </summary>
public class GroupRoleSemanticsTests
{
    /// <summary>What a reader meets, one stop at a time: the role, the name, and the two states a
    /// set of choices carries.</summary>
    private readonly record struct Stop(SemanticRole Role, string Label, bool? Selected, SemanticCheck? Checked);

    private static (PhotonHost Host, RealizeResult Frame) Render(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, frame);
    }

    private static List<Stop> Stops(PhotonHost host) =>
        host.Semantics().Select(node => new Stop(node.Role, node.Label, node.Selected, node.Checked)).ToList();

    [Fact]
    public void ATabStripIsATabBarWhoseTabsFollowItEachSayingWhetherItIsPicked()
    {
        var (host, _) = Render(new Tabs(["Overview", "Activity", "Settings"], 1, _ => { }));

        Stops(host).Should().Equal(
            [
                new Stop(SemanticRole.TabBar, "", null, null),
                new Stop(SemanticRole.Tab, "Overview", false, null),
                new Stop(SemanticRole.Tab, "Activity", true, null),
                new Stop(SemanticRole.Tab, "Settings", false, null),
            ],
            "the bar is read, then each tab in the order the strip draws it, picked or not, as the web's "
            + "tablist of tabs with aria-selected is");

        var bar = host.Semantics()[0];
        bar.Value.Should().BeNull("a tab bar holds a set of choices, not a position on a range");
        bar.Range.Should().BeNull();
    }

    [Fact]
    public void ARadioGroupIsAGroupWhoseRadiosFollowItEachWithItsCheck()
    {
        var (host, _) = Render(new RadioGroup(["Standard", "Express", "Overnight"], 1, _ => { }, "Shipping"));

        Stops(host).Should().Equal(
            [
                // The caption the component draws above its rows, a paragraph the web reads too.
                new Stop(SemanticRole.StaticText, "Shipping", null, null),
                new Stop(SemanticRole.RadioGroup, "Shipping", null, null),
                new Stop(SemanticRole.Radio, "Standard", null, SemanticCheck.Off),
                new Stop(SemanticRole.Radio, "Express", null, SemanticCheck.On),
                new Stop(SemanticRole.Radio, "Overnight", null, SemanticCheck.Off),
            ],
            "the group carries the name its author gave it, and each radio its check, as the web's "
            + "radiogroup with aria-label and its radios with aria-checked do");
    }

    /// <summary>The SegmentedControl rides the same mechanism as the RadioGroup, on both targets:
    /// its segments are radios and its track is their group.</summary>
    [Fact]
    public void ASegmentedControlIsARadioGroupToo()
    {
        var (host, _) = Render(new SegmentedControl(["All", "Income", "Expenses"], 0, _ => { }));

        Stops(host).Should().Equal(
            [
                new Stop(SemanticRole.RadioGroup, "", null, null),
                new Stop(SemanticRole.Radio, "All", null, SemanticCheck.On),
                new Stop(SemanticRole.Radio, "Income", null, SemanticCheck.Off),
                new Stop(SemanticRole.Radio, "Expenses", null, SemanticCheck.Off),
            ]);
    }

    /// <summary>
    /// The KEYBOARD did not move: the bar is still the one Tab stop and its arrows still step the
    /// pick. What changed is the READER's way through the same control, which is the tabs, each
    /// running the handler a tap runs.
    /// </summary>
    [Fact]
    public void TheBarIsTheOneKeyboardStopAndEachTabActsAsATapDoes()
    {
        var picked = new List<int>();
        var (host, frame) = Render(new Tabs(["Overview", "Activity", "Settings"], 1, picked.Add));
        var semantics = host.Semantics();
        var bar = semantics.Single(node => node.Role == SemanticRole.TabBar);
        var tabs = semantics.Where(node => node.Role == SemanticRole.Tab).ToList();

        frame.FocusStops.Select(stop => stop.Path).Should().Equal([bar.Path],
            "the strip is ONE Tab stop, the ARIA tablist pattern, and the tabs are not stops of their own");
        tabs.Should().OnlyContain(tab => tab.Path.StartsWith(bar.Path + "/", StringComparison.Ordinal),
            "what the bar holds is announced after it and under it, which is what walking into a container means");

        host.ActivatePath(tabs[2].Path).Should().BeTrue("a reader's double tap on a tab is a tap");
        picked.Should().Equal([2]);

        host.AdjustPath(bar.Path, +1).Should().BeTrue("and the arrows still step the pick from the bar");
        picked.Should().Equal([2, 2], "one step on from the picked tab, Activity, is Settings");
    }

    /// <summary>A disabled group takes no keyboard, so it mounts no Adjustable and is no group: its
    /// radios are read alone and dimmed, exactly as the web's disabled RadioGroup has no radiogroup
    /// wrapper and keeps its radios.</summary>
    [Fact]
    public void ADisabledRadioGroupIsItsRadiosAlone()
    {
        var (host, _) = Render(new RadioGroup(["A", "B"], 0, _ => { }) { Disabled = true });

        var semantics = host.Semantics();
        semantics.Should().NotContain(node => node.Role == SemanticRole.RadioGroup);
        semantics.Where(node => node.Role == SemanticRole.Radio).Should().HaveCount(2)
            .And.OnlyContain(radio => radio.Disabled, "what the rows ARE does not change with their availability");
    }
}
