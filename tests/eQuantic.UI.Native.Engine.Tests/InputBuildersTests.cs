using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A select and a segmented control draw their parts with the developer's builder (#1548, the catalog's
/// decision 5): a <c>Func&lt;int, VisualNode&gt;</c> over the part's index draws its CONTENT inside the
/// component's own chrome, the part's text stays its accessible name, and with no builder the output is what
/// it was. Photon's half; the web's is <c>InputBuildersRealizerTests</c>. The content is a swatch of a colour
/// of its own per index, so the frame says which index drew where.
/// </summary>
public class InputBuildersTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static ColorToken Swatch(int index) => new(Color.FromRgb((byte)(0x20 + index * 0x30), 0x77, 0xC3));

    private static VisualNode Drawn(int index) =>
        new Box(new BoxStyle { Width = 14, Height = 14, Background = Swatch(index) });

    private static (PhotonHost Host, DisplayList Frame) Render(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder());
        return (host, Frame(host));
    }

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 16);
        return builder.Build();
    }

    private static bool Paints(DisplayList list, int index)
    {
        var color = Swatch(index).Resolve(ThemeMode.Light, ContrastLevel.Standard);
        return list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color);
    }

    private static void Open(PhotonHost host)
    {
        var trigger = host.Semantics().Single(node => node.Expanded is not null);
        host.Activate(trigger.Id).Should().BeTrue();
    }

    private static Select Grouped(List<int> chosen, List<int>? asked = null, bool build = true) =>
        new(["Lisboa"], 0, chosen.Add, groups: [new OptionGroup("Next", ["Braga", "Faro"])],
            optionBuilder: build ? index =>
            {
                asked?.Add(index);
                return Drawn(index);
            } : null);

    [Fact]
    public void ASelectsOptions_AreDrawnByTheBuilder_OverOneIndexAcrossTheGroups()
    {
        var asked = new List<int>();
        var (host, _) = Render(Grouped([], asked));
        Open(host);
        var frame = Frame(host);

        asked.Distinct().Order().Should().Equal([0, 1, 2], "the ungrouped option is 0 and the group's are 1 and 2");
        Paints(frame, 0).Should().BeTrue();
        Paints(frame, 1).Should().BeTrue();
        Paints(frame, 2).Should().BeTrue();
        host.Semantics().Where(node => node.Role == SemanticRole.Option).Select(node => node.Label)
            .Should().Equal(["Lisboa", "Braga", "Faro"], "the option's text stays its accessible name");
    }

    [Fact]
    public void PickingAnOptionDrawnByTheBuilder_ReportsItsFlatIndex()
    {
        var chosen = new List<int>();
        var (host, _) = Render(Grouped(chosen));
        Open(host);
        Frame(host);

        var faro = host.Semantics().Single(node => node.Role == SemanticRole.Option && node.Label == "Faro");
        host.Activate(faro.Id).Should().BeTrue();

        chosen.Should().Equal(2);
    }

    [Fact]
    public void TheClosedField_ShowsTheChosenOptionsText_AndTheBuildersContentWaitsForTheOpenList()
    {
        var (host, frame) = Render(Grouped([]));

        Paints(frame, 0).Should().BeFalse("the closed field says the option's text, not its drawing");
        // The field's value is the option's text: its name is the field's label, which this one has none of.
        host.Semantics().Single().Value.Should().Be("Lisboa");
    }

    [Fact]
    public void WithNoBuilder_ASelectDrawsNoContentOfTheDevelopers()
    {
        var (host, _) = Render(Grouped([], build: false));
        Open(host);
        var frame = Frame(host);

        Paints(frame, 1).Should().BeFalse();
        host.Semantics().Where(node => node.Role == SemanticRole.Option).Select(node => node.Label)
            .Should().Equal(["Lisboa", "Braga", "Faro"]);
    }

    [Fact]
    public void ASegmentedControlsSegments_AreDrawnByTheBuilder_AndKeepTheirNames()
    {
        var chosen = new List<int>();
        var (host, frame) = Render(new SegmentedControl(["Day", "Week", "Month"], 0, chosen.Add, segmentBuilder: Drawn));

        Paints(frame, 0).Should().BeTrue();
        Paints(frame, 1).Should().BeTrue();
        Paints(frame, 2).Should().BeTrue();
        var segments = host.Semantics().Where(node => node.Role == SemanticRole.Radio).ToList();
        segments.Select(node => node.Label).Should().Equal("Day", "Week", "Month");

        host.Activate(segments[2].Id).Should().BeTrue();
        chosen.Should().Equal(2);
    }

    [Fact]
    public void WithNoBuilder_ASegmentedControlDrawsNoContentOfTheDevelopers()
    {
        var (host, frame) = Render(new SegmentedControl(["Day", "Week"], 0, _ => { }));

        Paints(frame, 0).Should().BeFalse();
        host.Semantics().Where(node => node.Role == SemanticRole.Radio).Select(node => node.Label)
            .Should().Equal(["Day", "Week"], "the segments are drawn, as their text");
    }

    /// <summary>With no platform text service a text draws as bars in its ink at 30%: the colour is what
    /// this reads (AForegroundDescendsTests).</summary>
    private static bool Inks(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard).WithOpacity(0.30f);
        return list.Commands.ToArray().Any(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color);
    }

    /// <summary>A builder's content that names no colour is drawn in the segment's: the chosen one's text
    /// colour and the others', as the segment's own text is.</summary>
    [Fact]
    public void ABuildersTextWithNoColour_TakesTheSegmentsColour()
    {
        var (_, frame) = Render(new SegmentedControl(["Day", "Week"], 0, _ => { },
            segmentBuilder: index => new Text(index == 0 ? "D" : "W", TypeRole.Label)));

        Inks(frame, Theme.TextPrimary).Should().BeTrue("the chosen segment's content");
        Inks(frame, Theme.TextSecondary).Should().BeTrue("and the other's, in the quieter colour");
    }
}
