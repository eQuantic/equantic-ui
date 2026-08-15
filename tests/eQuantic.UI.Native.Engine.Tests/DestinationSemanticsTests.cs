using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The NATIVE half of "where you are": a bar's active destination reaches the semantics tree as a
/// bit beside the name, which each bridge then reports with the nearest word its platform has —
/// the Selected trait on iOS, <c>accessibilitySelected</c> on macOS, <c>Selected</c> on Android's
/// node info. Cross-pinned with the web's DestinationSemanticsTests: the same tree, the same claim.
/// </summary>
public class DestinationSemanticsTests
{
    private static readonly NavItem[] Items =
    [
        new(Icons.Notifications, "Activity"),
        new(Icons.Search, "Search"),
        new(Icons.Person, "Profile"),
    ];

    private static IReadOnlyList<SemanticNode> SemanticsOf(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    [Fact]
    public void OnlyTheCurrentDestinationCarriesTheBit()
    {
        var nodes = SemanticsOf(new BottomNavigation(Items, 1, _ => { }))
            .Where(node => Items.Any(item => item.Label == node.Label))
            .ToList();

        nodes.Should().HaveCount(3);
        nodes.Where(node => node.Current).Should().ContainSingle()
            .Which.Label.Should().Be("Search");
    }

    /// <summary>The rail is the bar's twin: a wider window must not be a quieter one.</summary>
    [Fact]
    public void TheRailSaysExactlyWhatTheBarSays()
    {
        var bar = SemanticsOf(new BottomNavigation(Items, 2, _ => { }));
        var rail = SemanticsOf(new NavigationRail(Items, 2, _ => { }));

        static (string Label, bool Current) Active(IReadOnlyList<SemanticNode> nodes) =>
            nodes.Where(node => node.Current).Select(node => (node.Label, node.Current)).Single();

        Active(rail).Should().Be(Active(bar));
    }

    /// <summary>A check is never "current" — its state is its VALUE, and a control claiming both
    /// would announce twice. This is why the bit is separate rather than another Checked value.</summary>
    [Fact]
    public void AChecksStateIsNotADestination()
    {
        var nodes = SemanticsOf(new Checkbox(true, () => { }, "Terms"));

        nodes.Should().NotContain(node => node.Current);
    }
}
