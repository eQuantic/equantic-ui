using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What Photon KNOWS about a live region, and — stated as plainly — what it does not do with it yet.
///
/// <para>
/// The semantics tree carries the region as a named group at its bounds, with the urgency on the
/// node (<see cref="SemanticNode.Live"/>). That is the data an announcement needs, and it is where
/// this lands: announcing means noticing that a subtree CHANGED, and <c>PhotonHost.Semantics()</c>
/// is a snapshot of the current frame with nothing to compare against. Posting from the walk would
/// fire every frame — a reader repeating itself sixty times a second is a reader the user turns off.
/// </para>
///
/// <para>
/// So the fence is pinned rather than described: the urgency reaches the tree, and nothing else in
/// the vocabulary claims to be live. The day the frame-to-frame diff lands, the data is already
/// there and this file is where the post gets asserted.
/// </para>
/// </summary>
public class LiveRegionSemanticsTests
{
    private static IReadOnlyList<SemanticNode> Describe(VisualNode node)
    {
        var host = new PhotonHost(node, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    private static LiveRegion Region(LiveRegionUrgency urgency) =>
        new(new Text("saved", TypeRole.BodyM)) { Label = "Upload status", Urgency = urgency };

    [Theory]
    [InlineData(LiveRegionUrgency.Polite)]
    [InlineData(LiveRegionUrgency.Assertive)]
    public void TheUrgencyReachesTheSemanticsTree(LiveRegionUrgency urgency)
    {
        var semantics = Describe(Region(urgency));

        semantics.Should().ContainSingle(s => s.Live == urgency)
            .Which.Role.Should().Be(SemanticRole.Group,
                "the urgency rides on a NAMED GROUP — a leaf role would consume the very subtree "
                + "an announcement has to re-read");
    }

    /// <summary>
    /// A region that interrupts when nothing happened is worse than one that never speaks, so
    /// <c>Live</c> is the exception and not the rule. Asserted over a tree that holds every other
    /// kind of announcing node, because "nothing else is live" is only worth saying if something
    /// else was asked.
    /// </summary>
    [Fact]
    public void NothingElseClaimsToBeLive()
    {
        var column = new Column(gap: 0);
        column.Add(new Text("plain", TypeRole.BodyM));
        column.Add(new Pressable(new Text("press", TypeRole.BodyM), () => { }) { Label = "Press" });
        column.Add(new Progress(new Text("bar", TypeRole.BodyM)) { Label = "Uploading" });
        column.Add(new Adjustable(new Text("x", TypeRole.BodyM), _ => { }) { Label = "Volume" });
        column.Add(new Navigable([new Text("row", TypeRole.BodyM)], _ => { }) { Label = "July 2026" });
        column.Add(Region(LiveRegionUrgency.Polite));

        var semantics = Describe(column);

        semantics.Should().HaveCountGreaterThan(5, "the sample really does hold the other roles");
        semantics.Where(s => s.Live is not null).Should().ContainSingle(
            "only a live region is live — and the other announcing nodes are in this tree so that "
            + "says something");
    }

    /// <summary>
    /// The label is the region's NAME, not its announcement, and the difference matters on the one
    /// target where the announcement does not exist yet: without this the group would be an unnamed
    /// stop that VoiceOver walks past.
    /// </summary>
    [Fact]
    public void TheRegionIsNamed()
    {
        Describe(Region(LiveRegionUrgency.Polite))
            .Should().Contain(s => s.Label == "Upload status" && s.Role == SemanticRole.Group);
    }
}
