using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The six wrappers that stand between a child and its flex parent — Pressable, Hoverable, Link,
/// Adjustable, Progress and LiveRegion — carry the child's WIDTH CONTRACT, not half of it.
/// <para>
/// Each takes `width: 100%` from a Fill child, because a wrapper that hugged would collapse the
/// child's own 100% against a shrink-to-fit box. None of them took the child's MAX-WIDTH, so a
/// card that fills up to a limit became a full-width wrapper holding a narrower block: the card
/// hugged the left edge and the row's `justify-content: center` had a full-width item to centre,
/// which is nothing to centre.
/// </para>
/// <para>
/// Reported from a real page, measured there (wrapper 1392px against a card capped at 980), and
/// reduced to the case below by the site's own session. The SSR suite could not have seen it: the
/// markup is correct on both sides, and only the browser resolves what it means.
/// </para>
/// </summary>
public class WrapperLayoutTransparencyTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    /// <summary>A card that fills its row UP TO a limit — the shape every centred panel has.</summary>
    private static Box CappedCard() => new(new BoxStyle
    {
        Width = SizeValue.Fill,
        MaxWidth = 980,
        Height = SizeValue.Fill,
        Background = Theme.Surface,
    }, new Text("panel"));

    public static TheoryData<string, VisualNode> Wrappers() => new()
    {
        { "Pressable", new Pressable(CappedCard(), () => { }) },
        { "Hoverable", new Hoverable(CappedCard(), _ => { }) },
        { "DropTarget", new DropTarget(CappedCard(), _ => { }) },
        { "Link", new Link("/somewhere", CappedCard()) },
        { "Adjustable", new Adjustable(CappedCard(), _ => { }) },
        { "Progress", new Progress(CappedCard()) },
        { "LiveRegion", new LiveRegion(CappedCard()) },
        // The third role-bearing host, and the one that forwarded NEITHER half (#241): its
        // block div took the container's width while the sheet inside kept its own.
        { "SheetSurface", new SheetSurface(CappedCard(), new SheetController()) },
    };

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void AWrapperCarriesTheChildsCap_NotOnlyItsFill(string name, VisualNode wrapper)
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.Center };
        row.Add(wrapper);

        // The row's own element also says `width: 100%`, so the wrapper is taken by POSITION —
        // the row's single child — rather than by a style the parent happens to share.
        var lowered = Render(row).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("max-width: 980px",
            $"{name} stands in for its child in the row's layout, so it carries the whole width "
            + "contract — a wrapper that takes the fill and drops the cap is full width, and the "
            + "row has nothing left to centre");
    }

    [Theory]
    [MemberData(nameof(Wrappers))]
    public void AWrapperCarriesBothAxes(string name, VisualNode wrapper)
    {
        // Half a contract is its own divergence, and it bit twice in this PR from opposite sides:
        // the twin's Link was short a height, and this side's Adjustable was. A wrapper mirrors
        // the axes it stands in for, or the two targets lay the same tree out differently.
        var column = new Column(gap: 0) { Height = SizeValue.Fill };
        column.Add(wrapper);

        var lowered = Render(column).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("height: 100%",
            $"{name} stands in for a child that fills the cross axis");
    }

    /// <summary>
    /// BOTH walks reach a child THROUGH the wrappers that are not themselves in the sweep — which
    /// is the half the sweep above cannot see, because it puts one wrapper over a Fill child and
    /// never nests.
    /// <para>
    /// `Simulated`, `InFlow` and `InView` were in the TypeScript `fills` and not in the C# `Fills`.
    /// A missing arm does not throw: it answers `(false, false)`, which is exactly what a child
    /// that does not fill answers, so SSR served a hugging host while the hydrated runtime walked
    /// through and filled.
    /// <para>
    /// Then the FIX was half of one. Adding the three to `Fills` and not to `CapsAt` left the host
    /// taking the child's 100% and dropping its maximum — the same half-contract the Link had, now
    /// written down three times, and on a ROLE-BEARING host it announces a box wider than the bar
    /// it names. So this asserts both halves, and the cap is the half that came second.
    /// </para>
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Simulated")]
    [InlineData("InFlow")]
    [InlineData("InView")]
    public void TheWholeWidthContractReachesThroughAWrapperTheSweepDoesNotList(string inner)
    {
        var fill = new Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 320 }, new Text("x"));
        VisualNode wrapped = inner switch
        {
            "Simulated" => new Simulated(new SimulatedState(), fill),
            "InFlow" => new InFlow(fill),
            _ => new InView(fill, _ => { }),
        };

        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Progress(wrapped));

        var lowered = Render(row).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%",
            $"the Fill child is under a {inner}, and the progress host stands in for it — a walk "
            + "that stops at the wrapper reports (false, false), which is what a child that does "
            + "not fill reports, so the host hugs on the server and fills in the browser");

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("max-width: 320px",
            $"and the CAP comes through the same {inner} — adding the arm to `Fills` and not to "
            + "`CapsAt` is the half-contract this repository has now written down three times: the "
            + "host takes the child's 100% and drops its maximum, so a role-bearing one announces "
            + "a box wider than the bar it names");
    }

    [Fact]
    public void AChildWithNoCapIsUntouched()
    {
        // The pass-through only mirrors what the child declared. A plain Fill child must not
        // acquire a limit it never had.
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill }, new Text("x")), () => { }));

        var lowered = Render(row).Children.Should().ContainSingle().Which;

        lowered.Attributes.GetValueOrDefault("style", "").Should().Contain("width: 100%");
        lowered.Attributes.GetValueOrDefault("style", "").Should().NotContain("max-width");
    }

    /// <summary>
    /// A SafeArea carries its child's fill and cap on both axes, the insets inside the box it takes,
    /// as Photon measures it (the child's size, plus the insets around it). On the web it hugged:
    /// the Wallet's cards page stood at its content's width beside the dialog over it, its title
    /// bar's action in the middle of the window, and its rail stopped at the last destination.
    /// </summary>
    [Fact]
    public void ASafeAreaCarriesItsChildsWholeContract()
    {
        var column = new Column(gap: 0) { Height = SizeValue.Fill };
        column.Add(new SafeArea(CappedCard()));

        var style = Render(column).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "");

        style.Should().Contain("width: 100%").And.Contain("max-width: 980px").And.Contain("height: 100%");
        style.Should().Contain("box-sizing: border-box", "the insets go inside the 100%, not past it");
    }

    /// <summary>
    /// Through a COMPONENT, which is what a SafeArea usually holds (a rail, a page): only what the
    /// component built says whether it fills, so the area reads it off what its child lowered to.
    /// </summary>
    [Fact]
    public void ASafeAreaCarriesTheFillOfWhatAComponentBuilt()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        row.Add(new SafeArea(new TallStrip(), SafeEdges.Start | SafeEdges.Bottom));

        var style = Render(row).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "");

        style.Should().Contain("height: 100%", "the strip the component builds fills the row's height");
        style.Should().NotContain("width: 100%", "and states its own width, which the area does not stretch");
    }

    private sealed class TallStrip : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Box(new BoxStyle { Width = 80, Height = SizeValue.Fill }, new Text("rail"));
    }

    /// <summary>
    /// Around an AdaptiveNode, which lowers to display:contents gates with every arm mounted: the area
    /// fills an axis when EVERY arm does, since the page's media rules show one at a time. The
    /// Wallet's page is that shape (a bar on a phone, a rail on a desktop), and its rail stopped at
    /// the last destination because the area around the two arms hugged.
    /// </summary>
    [Fact]
    public void ASafeAreaAroundAnAdaptiveNode_FillsWhereEveryArmFills()
    {
        static VisualNode Arm(bool fills) => new Column(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = fills ? SizeValue.Fill : SizeValue.Hug,
        };

        var both = new Column(gap: 0) { Height = SizeValue.Fill };
        both.Add(new SafeArea(new AdaptiveNode(Arm(fills: true), Arm(fills: true)), SafeEdges.Top));
        var one = new Column(gap: 0) { Height = SizeValue.Fill };
        one.Add(new SafeArea(new AdaptiveNode(Arm(fills: true), Arm(fills: false)), SafeEdges.Top));

        Render(both).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "")
            .Should().Contain("height: 100%").And.Contain("width: 100%");
        Render(one).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "")
            .Should().NotContain("height: 100%", "an arm that hugs would stand in an area taller than it")
            .And.Contain("width: 100%");
    }

    /// <summary>
    /// A wrapper that reports rather than lays out lowers to one display:contents element around its
    /// child, which is no adaptive gate: the area reads the child itself through it, not the child's
    /// children. And a fill is the parent's extent, never more: the area carries the min size of 0
    /// that a filling row writes, so one long line cannot grow it.
    /// </summary>
    [Fact]
    public void ASafeAreaReadsThroughABoxlessWrapper_AndNeverGrowsPastItsParent()
    {
        var column = new Column(gap: 0) { Height = SizeValue.Fill };
        column.Add(new SafeArea(new InView(CappedCard(), _ => { })));

        var style = Render(column).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "");

        style.Should().Contain("width: 100%").And.Contain("height: 100%").And.Contain("max-width: 980px");
        style.Should().Contain("min-width: 0").And.Contain("min-height: 0");
    }

    [Fact]
    public void ASafeAreaAroundAChildThatHugs_HugsToo()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill };
        row.Add(new SafeArea(new Text("x")));

        var style = Render(row).Children.Should().ContainSingle().Which.Attributes.GetValueOrDefault("style", "");

        style.Should().NotContain("width: 100%").And.NotContain("height: 100%").And.NotContain("box-sizing");
    }

    /// <summary>
    /// The browser's `fills` walks every wrapper the vocabulary calls layout-transparent, the rule the
    /// C# `Fills` reads. It cannot read the rule, so its cases are compared to it here: a kind left
    /// out answers "does not fill", which is what a child that does not fill answers, and the served
    /// page and the hydrated one lay the same tree out differently, with nothing failing.
    /// </summary>
    [Fact]
    public void TheBrowsersFills_WalksEveryTransparentWrapper()
    {
        var lowering = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "lowering.ts"));
        var start = lowering.IndexOf("function fills(node: VisualNodeValue)", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "lowering.ts declares the browser's fills");
        var end = lowering.IndexOf("default:", start, StringComparison.Ordinal);
        var walked = System.Text.RegularExpressions.Regex.Matches(lowering[start..end], @"case '([A-Za-z]+)':")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var transparent = typeof(SingleChildNode).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(SingleChildNode)) && !type.IsAbstract)
            .Select(type => (SingleChildNode)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type))
            .Where(wrapper => wrapper.IsLayoutTransparent())
            .Select(wrapper => wrapper.NodeKind)
            .ToList();

        transparent.Should().NotBeEmpty();
        transparent.Where(kind => !walked.Contains(kind)).Should().BeEmpty(
            "every wrapper the vocabulary calls layout-transparent passes its child's fill through, on "
            + "the server and in the browser alike");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "eQuantic.UI.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("the repository root holds eQuantic.UI.sln");
    }
}
