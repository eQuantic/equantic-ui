using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A contained failure must not be a silent one. Reported by the eQuantic Code IDE, whose ENTIRE
/// title bar threw on every frame while every automated signal said the app was healthy: frames
/// presented, exit 0, and an accessibility count that went UP — the containment surface has text of
/// its own, so a broken window reports MORE elements than a working one.
/// <para>
/// The check that session had written down as the one that catches a black window could not catch a
/// black window. Containment is right for a shipped app and the SDK will keep doing it; what was
/// missing is that nothing an automated check reads ever mentioned it.
/// </para>
/// </summary>
public class ContainedIsNotSilentTests
{
    private sealed class Breaks : UiComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            throw new InvalidOperationException("icon size 11 is not on the whitelist");
    }

    private sealed class Fine : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("ok", TypeRole.BodyM);
    }

    private static void Render(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
    }

    public ContainedIsNotSilentTests() => ComponentBoundary.ClearContained();

    [Fact]
    public void AHealthyRender_ContainsNothing()
    {
        Render(new Fine());

        ComponentBoundary.Contained.Should().BeEmpty("empty is the answer a working app gives");
    }

    [Fact]
    public void AThrowingComponent_IsNamed_AndTheSiblingsStillRender()
    {
        var column = new Column(gap: 0);
        column.Add(new Breaks());
        column.Add(new Fine());

        Render(column);

        // The name is the actionable fact: a log line is for a human, this is for a check.
        ComponentBoundary.Contained.Should().Equal(["Breaks"]);
    }

    /// <summary>
    /// The shape of the real report: it threw on EVERY frame. A count would have reported the frame
    /// rate; the name is the same however many frames ran, which is why this records names.
    /// </summary>
    [Fact]
    public void ThrowingOnEveryFrame_IsStillOneName()
    {
        var root = new Breaks();
        for (var frame = 0; frame < 12; frame++) Render(root);

        ComponentBoundary.Contained.Should().Equal("Breaks");
    }

    /// <summary>
    /// The tally belongs to a RUN, and a run has to say when it starts or the tally is whatever
    /// survived whatever ran before it in the same process — which a long-lived host (the design
    /// host runs many apps) reaches immediately. `PhotonApplication.Run` arms it.
    /// <para>
    /// Within one run a component that threw and then recovered still counts, and that is the
    /// point rather than a rough edge: it DID throw, and a summary that hides a failure because it
    /// stopped is the silence this whole change exists to remove. The class contract's "no failure
    /// is remembered" is about BEHAVIOUR — the recovered component builds and draws exactly like
    /// one that never threw — and this is the diagnostic, which remembers until the run ends.
    /// </para>
    /// </summary>
    [Fact]
    public void ARecoveredComponent_StillCountedForThatRun_ButNotTheNext()
    {
        var flaky = new Breaks();
        Render(flaky);
        Render(new Fine());
        ComponentBoundary.Contained.Should().Equal(["Breaks"], "it threw, and recovering does not unthrow it");

        // A new run arms the tally, the way PhotonApplication.Run does.
        ComponentBoundary.ClearContained();
        Render(new Fine());

        ComponentBoundary.Contained.Should().BeEmpty("a healthy run is not accused of the last one's failure");
    }
}
