using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A select and a segmented control draw their parts with the developer's builder (#1548, the catalog's
/// decision 5): a <c>Func&lt;int, VisualNode&gt;</c> over the part's index draws its CONTENT inside the
/// component's own chrome, the part's text stays its accessible name, and with no builder the output is what
/// it was. The web's half; Photon's is <c>InputBuildersTests</c>.
/// </summary>
public class InputBuildersRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static VisualNode Drawn(int index) => new Text("drawn-" + index, TypeRole.BodyM);

    /// <summary>An ungrouped option and a group of two: three options, counted 0, 1 and 2.</summary>
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
        var chosen = new List<int>();
        var asked = new List<int>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(Grouped(chosen, asked));
        tester.Tap(SemanticRole.ComboBox, "Lisboa");
        tester.Pump();

        asked.Distinct().Order().Should().Equal([0, 1, 2], "the ungrouped option is 0 and the group's are 1 and 2");
        tester.Html.Should().Contain("drawn-0").And.Contain("drawn-1").And.Contain("drawn-2");
        tester.Semantics.Where(stop => stop.Role == SemanticRole.Option).Select(stop => stop.Name)
            .Should().Equal(["Lisboa", "Braga", "Faro"], "the option's text stays its accessible name");
    }

    [Fact]
    public void PickingAnOptionDrawnByTheBuilder_ReportsItsFlatIndex()
    {
        var chosen = new List<int>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(Grouped(chosen));
        tester.Tap(SemanticRole.ComboBox, "Lisboa");
        tester.Pump();

        tester.Tap(SemanticRole.Option, "Faro");

        chosen.Should().Equal(2);
    }

    [Fact]
    public void TheClosedField_ShowsTheChosenOptionsText_NotTheBuilders()
    {
        var chosen = new List<int>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(Grouped(chosen));

        tester.Html.Should().Contain("Lisboa").And.NotContain("drawn-");
    }

    [Fact]
    public void WithNoBuilder_ASelectDrawsItsOptionsAsText()
    {
        var chosen = new List<int>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(Grouped(chosen, build: false));
        tester.Tap(SemanticRole.ComboBox, "Lisboa");
        tester.Pump();

        tester.Html.Should().Contain("Braga").And.NotContain("drawn-");
    }

    [Fact]
    public void ASegmentedControlsSegments_AreDrawnByTheBuilder_AndKeepTheirNames()
    {
        var chosen = new List<int>();
        using var tester = new ComponentTester(Theme);
        tester.Pump(new SegmentedControl(["Day", "Week", "Month"], 0, chosen.Add, segmentBuilder: Drawn));

        tester.Html.Should().Contain("drawn-0").And.Contain("drawn-1").And.Contain("drawn-2");
        tester.Semantics.Where(stop => stop.Role == SemanticRole.Radio).Select(stop => stop.Name)
            .Should().Equal("Day", "Week", "Month");

        tester.Tap(SemanticRole.Radio, "Month");
        chosen.Should().Equal(2);
    }

    [Fact]
    public void WithNoBuilder_ASegmentedControlDrawsItsSegmentsAsText()
    {
        using var tester = new ComponentTester(Theme);
        tester.Pump(new SegmentedControl(["Day", "Week"], 0, _ => { }));

        tester.Html.Should().Contain("Week").And.NotContain("drawn-");
    }
}
