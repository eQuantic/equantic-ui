using System.Text;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Email.Tests;

/// <summary>
/// A cycle FAILS THE SEND rather than the process (#223).
///
/// <para>
/// #221 bounded the component chain for the realizers that CONTAIN a failure, and left this one out
/// deliberately: email expands through <c>Build</c> rather than <c>BuildContained</c>, because a
/// broken component must fail the send instead of reaching an inbox dressed as a describe-box. The
/// bound was read as part of that divergence, and it is not — so a cyclic component walked back into
/// Build forever and died on a <c>StackOverflowException</c>.
/// </para>
///
/// <para>
/// THE A/B IS NOT AN ASSERTION, because the failure it replaces cannot be asserted: .NET does not
/// let anyone catch a stack overflow, so with the bound removed these do not fail, they ABORT the
/// test host — "Test Run Aborted", with a trace alternating <c>EmailVisitor.Visit</c> and
/// <c>UiComponent.Accept</c>. That is the shape #221 measured for the other two realizers and the
/// shape reproduced here before the fix.
/// </para>
///
/// <para>
/// So what these assert is the REPLACEMENT: an exception the caller can catch, naming the component,
/// leaving the render without a message. Nothing about the send path is mocked — the bound is what
/// turns an uncatchable death into an ordinary failure, and the rest is the app's to handle.
/// </para>
/// </summary>
public class ComponentCycleTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>
    /// How many components the boundary allows to nest. Private there and quoted in the message it
    /// throws, so it is written here once rather than spelled into each case — and the pair of
    /// chains below is what would fail if it ever moved, which is the right way to find out.
    /// </summary>
    private const int Bound = 64;

    /// <summary>
    /// A component whose Build returns <c>this</c> — the shape the web and native cycle tests pin, so
    /// the three realizers are asked the same question.
    /// </summary>
    private sealed class BuildsItself : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => this;
    }

    /// <summary>
    /// The other self-cycle: a FRESH instance every time. The same infinite chain to a realizer that
    /// expands what Build returned, and a different one to anything that recognises a node by identity —
    /// which is why both are asked rather than only the tidier one.
    /// </summary>
    private sealed class AllocatesAnother : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new AllocatesAnother();
    }

    /// <summary>
    /// A FINITE chain of exactly <paramref name="remaining"/> components, ending in a subtree. Nothing
    /// about it is cyclic: it is how the bound's edges are asked, and how the root expansion is asked
    /// whether it counts itself.
    /// </summary>
    private sealed class Chain(int remaining) : UiComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            remaining > 0 ? new Chain(remaining - 1) : new Text("the chain ended", TypeRole.BodyM);
    }

    /// <summary>Two that build each other: the same chain, with nothing in either type to see it.</summary>
    private sealed class BuildsTheOther : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new BuildsTheFirst();
    }

    private sealed class BuildsTheFirst : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new BuildsTheOther();
    }

    /// <summary>An ORDINARY component: one expansion and it reaches a subtree.</summary>
    private sealed class BuildsAGreeting : UiComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("after two cycles", TypeRole.BodyM);
    }

    /// <summary>
    /// BOTH self-cycles: the one that returns <c>this</c>, which is the shape the web and native
    /// suites pin, and the one that allocates a fresh instance every time. They are the same
    /// infinite chain to a realizer that expands what Build returned, and asking only the tidier one
    /// would leave the other to be assumed.
    /// </summary>
    [Theory]
    [InlineData(nameof(BuildsItself))]
    [InlineData(nameof(AllocatesAnother))]
    public void AComponentThatBuildsItself_FailsTheRenderAndIsNamed(string shape)
    {
        UiComponent cyclic = shape == nameof(BuildsItself) ? new BuildsItself() : new AllocatesAnother();

        var failure = Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(cyclic, Theme));

        failure.Message.Should().Contain(shape,
            "the one actionable fact is WHICH component never reached a subtree");
        failure.Message.Should().Contain("builds itself",
            "the bound observes DEPTH, so it names every shape that reaches it rather than "
            + "diagnosing a cycle it cannot distinguish from a tree nested too deep");
    }

    /// <summary>
    /// THE ROOT EXPANSION COUNTS ITSELF, asked with a finite chain rather than a cycle — which is
    /// the only way to ask it. Review found the gap and it was real: with the root's scope removed,
    /// every cycle test above stays green, because the visitors enter each component of the tree the
    /// root returned and a cycle exceeds any bound whether or not one level was counted.
    ///
    /// <para>
    /// A chain of exactly <see cref="Bound"/> components renders; one component longer does not —
    /// and that last one is over the line ONLY because the root was counted. Both edges are here on
    /// purpose: the first would pass if the bound were merely lower, and the second if it were
    /// merely absent.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRootExpansionCountsItself()
    {
        EmailRenderer.Render(new Chain(Bound - 1), Theme)
            .Html.Should().Contain("the chain ended",
                $"{Bound} components is what the boundary allows, and this chain is exactly that");

        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(new Chain(Bound), Theme))
            .Message.Should().Contain(nameof(Chain),
                "one more component is one more than the bound — and it only is if the root, which "
                + "the renderer expands rather than a visitor, entered the boundary like the rest");
    }

    /// <summary>
    /// DISPOSING TWICE PUTS BACK THE SAME NUMBER. The scope is a public value type, so a copy of it
    /// disposed beside the original is a thing a caller can write by accident — a struct assigns by
    /// value — and a scope that DECREMENTED would then leave the counter below the walk's real
    /// depth. Far enough below, the bound stops being reached at all: the stack overflow this exists
    /// to replace, arriving through the thing that replaced it.
    /// </summary>
    [Fact]
    public void DisposingTheSameScopeTwice_LeavesTheDepthWhereItWas()
    {
        var component = new BuildsAGreeting();

        var scope = ComponentBoundary.Enter(component);
        var copy = scope;
        scope.Dispose();
        copy.Dispose();

        // If either disposal had decremented, the depth would now be -1 and a chain one longer than
        // the bound would fit inside it.
        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(new Chain(Bound), Theme));
        EmailRenderer.Render(new Chain(Bound - 1), Theme).Html.Should().Contain("the chain ended");
    }

    /// <summary>
    /// A cycle of two, where the component NAMED is whichever one stood at the bound — not the one
    /// the render started from. My first draft asserted the starting type and was wrong about the
    /// code rather than the other way round: the message reports what was observed, and what was
    /// observed is the component that was still building at depth 64. Which of the pair that is
    /// depends on the parity of the bound, which is not a fact worth pinning.
    /// </summary>
    [Fact]
    public void ACycleOfTwo_FailsTheRenderAndNamesOneOfThem()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => EmailRenderer.Render(new BuildsTheOther(), Theme));

        failure.Message.Should().Match(
            m => m.Contains(nameof(BuildsTheOther)) || m.Contains(nameof(BuildsTheFirst)),
            "neither type can see the cycle from inside itself, so the bound is the only thing that "
            + "can name a participant at all");
    }

    /// <summary>
    /// BOTH ALTERNATIVES, asked directly, for the reason the csproj gives for making them visible at
    /// all: a whole-message test cannot see the text walk, because the HTML part is built first and
    /// throws before it runs. A bound on one of them only would be exactly the divergence
    /// <c>EmailWalk</c> exists to prevent — the two parts of one message disagreeing about the tree.
    /// </summary>
    [Fact]
    public void BothAlternativesRefuseTheCycle()
    {
        Assert.Throws<InvalidOperationException>(() =>
                new BuildsItself().Accept(new EmailVisitor(new ComponentContext(Theme), new StringBuilder()), default))
            .Message.Should().Contain(nameof(BuildsItself));

        Assert.Throws<InvalidOperationException>(() =>
                new BuildsItself().Accept(new EmailTextVisitor(Theme, new StringBuilder()), default))
            .Message.Should().Contain(nameof(BuildsItself),
                "the plain-text alternative walks the same tree, and a bound on one part of a "
                + "message only would send the other half of it");
    }

    /// <summary>
    /// The scope decrements on the way back up, INCLUDING when the throw unwinds through it —
    /// otherwise the counter keeps the depth of the deepest failure and a later, ordinary render is
    /// refused on a budget it never spent.
    /// <para>
    /// The render that follows has to contain a COMPONENT, and a first draft of this used a bare
    /// <c>Text</c>: nothing in it ever enters the boundary, so it rendered happily with the counter
    /// stuck at the bound and the test passed with <c>Dispose</c> neutered. It asserted the
    /// aftermath of a thing it never touched.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDepthIsRestored_SoOneFailedRenderDoesNotPoisonTheNext()
    {
        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(new BuildsItself(), Theme));
        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(new BuildsItself(), Theme));

        var message = EmailRenderer.Render(new BuildsAGreeting(), Theme);

        message.Html.Should().Contain("after two cycles");
        message.PlainText.Should().Contain("after two cycles",
            "the text alternative spends the same budget on the same tree");
    }
}
