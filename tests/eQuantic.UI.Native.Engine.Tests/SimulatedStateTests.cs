using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The native half of <c>Simulated</c> — the same node, the same states, drawn as GPU commands.
/// <para>
/// A feature that only worked on the web would not be a feature of this framework: a gallery
/// showing a hovered control on a documentation site and a rest control in the desktop app is two
/// answers to one question.
/// </para>
/// </summary>
public class SimulatedStateTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DisplayList Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 200, 100, Theme, ThemeMode.Light, builder);
        return builder.Build();
    }

    private static Box Hoverable() => new(new BoxStyle
    {
        Width = 100,
        Height = 40,
        Background = Theme.Surface,
        Hover = new StyleDiff { Background = Theme.Colors(Variant.Primary).Base },
    }, null);

    private static bool Paints(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light);
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color) return true;
        return false;
    }

    [Fact]
    public void AHoveredSubtree_PaintsItsHoverColour()
    {
        Paints(Draw(Hoverable()), Theme.Colors(Variant.Primary).Base)
            .Should().BeFalse("nothing is hovered");

        Paints(Draw(new Simulated(SimulatedState.Hovered, Hoverable())), Theme.Colors(Variant.Primary).Base)
            .Should().BeTrue("the picture says it is");
    }

    [Fact]
    public void APressedSubtree_PaintsItsPressedFill()
    {
        var pressable = new Pressable(new Box(new BoxStyle
        {
            Width = 100,
            Height = 40,
            Background = Theme.Surface,
        }, null))
        {
            PressedBackground = Theme.Colors(Variant.Primary).Base,
        };

        Paints(Draw(pressable), Theme.Colors(Variant.Primary).Base).Should().BeFalse();
        Paints(Draw(new Simulated(SimulatedState.Pressed, pressable)), Theme.Colors(Variant.Primary).Base)
            .Should().BeTrue();
    }

    /// <summary>The state does not LEAK past the node — a preview beside an ordinary control has to
    /// leave that control alone, or a gallery row would light up entirely.</summary>
    [Fact]
    public void TheStateEndsWithTheNode()
    {
        var row = new Row(gap: 0)
        {
            new Simulated(SimulatedState.Hovered, Hoverable()),
            Hoverable(),
        };

        var list = Draw(row);
        var hoverColour = Theme.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        var surface = Theme.Surface.Resolve(ThemeMode.Light);

        var hovered = 0;
        var resting = 0;
        foreach (var command in list.Commands)
        {
            if (command.Kind != DrawCommandKind.FillRRect) continue;
            if (command.Paint.Color == hoverColour) hovered++;
            else if (command.Paint.Color == surface) resting++;
        }

        hovered.Should().Be(1, "exactly the previewed one");
        resting.Should().BeGreaterThan(0, "and the sibling kept its rest colour");
    }
}
