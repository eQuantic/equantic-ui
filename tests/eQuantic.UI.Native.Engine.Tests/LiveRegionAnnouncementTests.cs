using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// THE HALF #246 LEFT OUT: a live region that ANNOUNCES, rather than one that merely carries the
/// urgency. <c>LiveRegionSemanticsTests</c> pins the data reaching the tree; this pins what is said
/// and — just as much — what is NOT said, because every failure mode of this feature is a reader
/// talking when it should be quiet.
/// </summary>
public class LiveRegionAnnouncementTests
{
    /// <summary>A host that keeps its frames, so a mark's node is not recycled under the diff.</summary>
    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);

    private static IReadOnlyList<LiveAnnouncement> Frame(PhotonHost host, float timeMs)
    {
        host.RenderFrame(new DisplayListBuilder(), timeMs);
        return host.TakeAnnouncements();
    }

    private sealed class Status(string text, LiveRegionUrgency urgency = LiveRegionUrgency.Polite)
        : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new LiveRegion(new Text(text, TypeRole.BodyM)) { Label = "Upload status", Urgency = urgency };
    }

    /// <summary>
    /// A region that APPEARS says nothing. It already reads through the ordinary semantics tree, and
    /// announcing on arrival would make every page load speak its own status line — the single
    /// loudest way to get a reader turned off.
    /// </summary>
    [Fact]
    public void TheFirstFrameAnnouncesNothing_BecauseNothingChangedYet()
    {
        Frame(Host(new Status("Idle")), 0).Should().BeEmpty(
            "arriving is not changing, and a page that announces itself on load is one a user "
            + "silences");
    }

    /// <summary>The whole feature, in one case: the text differs from last frame, so it is said.</summary>
    [Fact]
    public void AChangedRegionIsAnnounced_WithItsTextItsNameAndItsUrgency()
    {
        var root = new Mutable("Uploading 10%");
        var host = Host(root);
        Frame(host, 0).Should().BeEmpty();

        root.Text = "Upload complete";
        var said = Frame(host, 2000);

        said.Should().ContainSingle().Which.Should().BeEquivalentTo(new LiveAnnouncement(
            said[0].Path, "Upload status", "Upload complete", LiveRegionUrgency.Polite));
    }

    /// <summary>
    /// AND AN UNCHANGED ONE IS NOT. This is the case the issue is actually about: the semantics
    /// walk runs every frame and would post every frame, and "a reader repeating itself sixty times
    /// a second is a reader the user turns off".
    /// </summary>
    [Fact]
    public void ASteadyRegionIsSilent_HoweverManyFramesPass()
    {
        var host = Host(new Status("Idle"));
        for (var frame = 0; frame < 30; frame++)
            Frame(host, frame * 16.7f).Should().BeEmpty(
                "nothing changed on frame {0}, and a diff that fires anyway is worse than no diff",
                frame);
    }

    /// <summary>
    /// THE DEBOUNCE, which is not the urgency. A percentage that ticks every frame changes every
    /// frame — the diff is doing its job and the answer is still no. Assertive says how hard an
    /// announcement interrupts, never how often one may be made, so both urgencies are asserted.
    /// </summary>
    [Theory]
    [InlineData(LiveRegionUrgency.Polite)]
    [InlineData(LiveRegionUrgency.Assertive)]
    public void ARegionThatChangesEveryFrameAnnouncesOnce_PerQuietWindow(LiveRegionUrgency urgency)
    {
        var root = new Mutable("0%", urgency);
        var host = Host(root);
        Frame(host, 0);

        var said = 0;
        for (var frame = 1; frame <= 30; frame++)
        {
            root.Text = $"{frame}%";
            said += Frame(host, frame * 16.7f).Count;
        }

        // 30 frames at 16.7ms is 501ms — inside one 1000ms window, so exactly one gets through.
        said.Should().Be(1, "the urgency is not a rate limit, and thirty announcements in half a "
            + "second is a reader nobody can listen to");
    }

    /// <summary>And the window OPENS again — a debounce that never lets go is a mute.</summary>
    [Fact]
    public void TheQuietWindowExpires_AndTheRegionSpeaksAgain()
    {
        var root = new Mutable("0%");
        var host = Host(root);
        Frame(host, 0);

        root.Text = "50%";
        Frame(host, 100).Should().ContainSingle();
        root.Text = "60%";
        Frame(host, 200).Should().BeEmpty("still inside the window");
        root.Text = "100%";
        Frame(host, 100 + LiveRegionAnnouncerQuietMs).Should().ContainSingle(
            "the window is a pause, not a mute");
    }

    /// <summary>
    /// What it says when the window opens is the CURRENT text, not the one that was pending while
    /// it was shut. A reader saying "60%" at a bar reading 100% is wrong in the way that makes
    /// people distrust the whole feature.
    /// </summary>
    [Fact]
    public void WhatItSaysIsWhatTheRegionSaysNow_NotWhatItSaidWhileQuiet()
    {
        var root = new Mutable("0%");
        var host = Host(root);
        Frame(host, 0);

        root.Text = "50%";
        Frame(host, 100);
        root.Text = "60%";
        Frame(host, 200);
        root.Text = "100%";

        Frame(host, 100 + LiveRegionAnnouncerQuietMs).Single().Text.Should().Be("100%",
            "the announcement is the region's state now, not a queue of what it passed through");
    }

    /// <summary>
    /// DRAINED, not read. A bridge asks after each frame; asking twice must not say it twice, or
    /// the sixty-times-a-second failure arrives from the polling end instead of the diffing end.
    /// </summary>
    [Fact]
    public void TakingTheAnnouncementsEmptiesThem()
    {
        var root = new Mutable("Idle");
        var host = Host(root);
        Frame(host, 0);
        root.Text = "Done";

        host.RenderFrame(new DisplayListBuilder(), 2000);
        host.TakeAnnouncements().Should().ContainSingle();
        host.TakeAnnouncements().Should().BeEmpty("an announcement is an event, not a state");
    }

    /// <summary>
    /// A region that GOES AWAY and comes back says itself again. The user was not there for the
    /// first one — a toast that reappears with the same words is a new event, not a repeat.
    /// </summary>
    [Fact]
    public void ARegionThatLeavesAndReturnsIsAnnouncedAgain()
    {
        var root = new Toggleable("Saved");
        var host = Host(root);
        Frame(host, 0);

        root.Shown = false;
        Frame(host, 2000).Should().BeEmpty("going away is not an announcement");
        root.Shown = true;
        Frame(host, 4000).Should().ContainSingle()
            .Which.Text.Should().Be("Saved", "coming back is a new event for whoever missed it");
    }

    /// <summary>
    /// A REGION THAT APPEARS MID-SESSION IS THE EVENT — the case `LiveRegion` was added for. B18 is
    /// a Banner that appeared and announced nothing; C4 is a Toast lowered to a non-modal Overlay,
    /// which announced nothing either. A rule that called arrival "no change" would leave both of
    /// them exactly as silent as before, through the feature built to fix them.
    /// <para>
    /// Measured: the first version of the announcer did precisely that, and this case is what
    /// caught it. Mutation: treat an unknown path as a seed on every frame rather than only on the
    /// first, and this fails while every other case still passes — which is why it is here and not
    /// folded into the return-visit one.
    /// </para>
    /// </summary>
    [Fact]
    public void AToastThatAppearsIsAnnounced_BecauseAppearingIsWhatItDoes()
    {
        var root = new Toggleable("Saved to drafts") { Shown = false };
        var host = Host(root);
        Frame(host, 0).Should().BeEmpty("nothing is live on the first frame");

        root.Shown = true;

        Frame(host, 2000).Should().ContainSingle(
            "a toast appearing IS the announcement — that is the whole of B18 and C4")
            .Which.Text.Should().Be("Saved to drafts");
    }

    /// <summary>
    /// And not on the FIRST frame, which is the one exception. Every region is new on frame one by
    /// definition, so announcing there would make a page read its own status line aloud on load —
    /// the loudest possible way to get a reader switched off.
    /// </summary>
    [Fact]
    public void ARegionPresentOnTheFirstFrameIsSeeded_NotAnnounced()
    {
        var host = Host(new Toggleable("Saved to drafts"));
        Frame(host, 0).Should().BeEmpty();
        Frame(host, 2000).Should().BeEmpty("it never changed; frame one taught the announcer what "
            + "it says, it did not make it say it");
    }

    /// <summary>
    /// A tree with no live region costs nothing and says nothing — the case almost every frame of
    /// almost every app is in, and the reason this feature does not tax an animation.
    /// </summary>
    [Fact]
    public void ATreeWithNoLiveRegionNeverAnnounces()
    {
        var host = Host(new Column(gap: 0));
        for (var frame = 0; frame < 10; frame++) Frame(host, frame * 16.7f).Should().BeEmpty();
    }

    /// <summary>
    /// ONE REGION REPLACED BY ANOTHER, with the count never moving. Distinct from the
    /// leave-and-return case beside it, where the tree holds none for a frame: here it always holds
    /// exactly one, and only the path changes.
    /// <para>
    /// NO MUTATION PROVES THIS ONE, said plainly rather than implied. It was written for a review
    /// finding that a <c>_said.Count == marks.Count</c> shortcut in <c>Forget</c> would strand the
    /// departed path — and it passed before the shortcut was removed, because the loop writes the
    /// ARRIVING path into <c>_said</c> before <c>Forget</c> runs, so the counts differ exactly when
    /// there is something to forget. The shortcut went anyway (it bought a skipped scan over a
    /// handful of keys and cost an unstated "paths are distinct" invariant), and this case stays
    /// because it pins behaviour nothing else does — not because it caught anything.
    /// </para>
    /// </summary>
    [Fact]
    public void ARegionSwappedForAnotherIsAnnouncedOnItsReturn()
    {
        var root = new Swappable();
        var host = Host(root);
        Frame(host, 0);

        root.Which = "b";                       // one region leaves, one arrives — count stays 1
        Frame(host, 2000).Should().ContainSingle().Which.Text.Should().Be("b");

        root.Which = "a";                       // the first one returns, saying what it said before
        Frame(host, 4000).Should().ContainSingle(
            "it left and came back, so it is a new arrival — and a count that did not move is not "
            + "evidence that the set did not")
            .Which.Text.Should().Be("a");
    }

    /// <summary>Two regions at different paths; which one is present swaps, and the count does not.</summary>
    private sealed class Swappable : StatefulComponent
    {
        public string Which { get; set; } = "a";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0);
            if (Which == "a") column.Add(new LiveRegion(new Text("a", TypeRole.BodyM)) { Label = "A" });
            else column.Add(new Text("spacer", TypeRole.BodyM));
            if (Which == "b") column.Add(new LiveRegion(new Text("b", TypeRole.BodyM)) { Label = "B" });
            return column;
        }
    }

    /// <summary>
    /// THE QUEUE IS BOUNDED, because a shell that never drains must not grow for the life of the
    /// app. `PhotonHost` is shared by every target and only the macOS bridge drains today — iOS and
    /// Android run the same host with no bridge wired, so an unbounded queue would leak on exactly
    /// the two platforms that cannot yet use the feature.
    /// <para>
    /// The OLDEST goes when the bound is reached, which is the same judgement the quiet window
    /// already makes: an announcement is an event whose worth expires, and what a reader wants is
    /// the latest state rather than the backlog. Asserted by never draining — the shape of the
    /// shells this is about.
    /// </para>
    /// <para>Mutation: drop the `RemoveAt(0)` bound and the queue grows past it, failing here.</para>
    /// </summary>
    [Fact]
    public void ThePendingQueueIsBounded_ForTheShellsThatNeverDrainIt()
    {
        var root = new Mutable("0");
        var host = Host(root);
        host.RenderFrame(new DisplayListBuilder(), 0);

        // 200 changes, each past the quiet window so every one queues, and NOTHING is drained.
        for (var step = 1; step <= 200; step++)
        {
            root.Text = $"{step}";
            host.RenderFrame(new DisplayListBuilder(), step * (LiveRegionAnnouncerQuietMs + 1));
        }

        var drained = host.TakeAnnouncements();
        drained.Should().HaveCount(32, "the queue holds a bound, not a history");
        drained[^1].Text.Should().Be("200", "and what survives is the newest — the state a reader "
            + "would want if it could only hear one");
    }

    private const float LiveRegionAnnouncerQuietMs = 1000f;

    /// <summary>A region whose text a test can change between frames.</summary>
    private sealed class Mutable(string text, LiveRegionUrgency urgency = LiveRegionUrgency.Polite)
        : StatefulComponent
    {
        public string Text { get; set; } = text;

        public override VisualNode Build(ComponentContext context) =>
            new LiveRegion(new Text(Text, TypeRole.BodyM))
                { Label = "Upload status", Urgency = urgency };
    }

    /// <summary>A region a test can remove from the tree and put back.</summary>
    private sealed class Toggleable(string text) : StatefulComponent
    {
        public bool Shown { get; set; } = true;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0);
            column.Add(new Text("page", TypeRole.BodyM));
            if (Shown) column.Add(new LiveRegion(new Text(text, TypeRole.BodyM)) { Label = "Save" });
            return column;
        }
    }
}
