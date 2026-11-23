using eQuantic.UI.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A radio group's option builder on Photon (#1606, the design's decision 5): the builder draws what an
/// option shows, and the group keeps its chrome, the ring, the row a press lands on and the option's
/// name, which stays its accessible name. The content takes the option's colour where it names none. The
/// web's half is in <c>RadioGroupRealizerTests</c>.
/// </summary>
public class RadioGroupOptionBuilderTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly string[] Names = ["Standard", "Express"];
    private static readonly string[] Shown = ["Standard, in five days", "Express, tomorrow"];

    private static RadioGroup Group(bool disabled = false) =>
        new(Names, 1, _ => { }, "Shipping", optionBuilder: index => new Text(Shown[index]))
        {
            Disabled = disabled,
        };

    [Fact]
    public void TheBuilderDrawsTheContent_AndTheNamesStayTheRadiosNames()
    {
        var host = new PhotonHost(Group(), Theme, ThemeMode.Light, 400, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        host.Semantics().Where(node => node.Role == SemanticRole.Radio).Select(node => node.Label)
            .Should().Equal(Names, "an option's name is its accessible name, whatever the builder draws");

        var drawn = new List<string>();
        Texts(frame.Root, drawn);
        drawn.Should().Contain(Shown, "the builder draws what each option shows");
        drawn.Should().NotContain(Names, "the names are not drawn beside the builder's content");
    }

    [Fact]
    public void TheBuildersText_TakesTheOptionsColour()
    {
        foreach (var (disabled, colour) in new[] { (false, Theme.TextPrimary), (true, Theme.TextMuted) })
        {
            Paints(Draw(Group(disabled)), colour).Should().BeTrue(
                $"a text the builder gives no colour draws in the option's {(disabled ? "muted" : "primary")} text");
        }
    }

    private static void Texts(LayoutNode node, List<string> found)
    {
        if (node.Source is Text { Content: var content }) found.Add(content);
        foreach (var child in node.Children) Texts(child, found);
    }

    /// <summary>With no platform text service, a text draws as bars in its ink at 30%
    /// (<c>AForegroundDescendsTests</c>).</summary>
    private static bool Paints(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard).WithOpacity(0.30f);
        return list.Commands.Any(command => command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color);
    }

    private static DisplayList Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 400, Theme, ThemeMode.Light, ContrastLevel.Standard, builder);
        return builder.Build();
    }
}
