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
        tester.Tap(SemanticRole.ComboBox, tester.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
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
        tester.Tap(SemanticRole.ComboBox, tester.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
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
        tester.Tap(SemanticRole.ComboBox, tester.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
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

    /// <summary>The colour rule nearest above the element that writes <paramref name="text"/>, itself
    /// included: what a text that names no colour is drawn in.</summary>
    private static string? ColourOf(ComponentTester tester, string text)
    {
        var rules = System.Text.RegularExpressions.Regex.Matches(tester.Css, @"\.(eq-[a-z0-9]+)\{color:([^}]*)\}")
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var path = new List<HtmlNode>();
        // An open list's options, ahead of the field that also writes the chosen one's text.
        var root = Listbox(tester.Root) ?? tester.Root;
        if (!Find(root, text, path)) throw new InvalidOperationException($"Nothing writes '{text}'.");
        for (var at = path.Count - 1; at >= 0; at--)
            foreach (var name in (path[at].Attributes.GetValueOrDefault("class") ?? "").Split(' '))
                if (rules.TryGetValue(name, out var colour)) return colour;
        return null;

        static HtmlNode? Listbox(HtmlNode node) =>
            node.Attributes.GetValueOrDefault("role") == "listbox"
                ? node
                : node.Children.Select(Listbox).FirstOrDefault(found => found is not null);

        static bool Find(HtmlNode node, string text, List<HtmlNode> path)
        {
            path.Add(node);
            if (node.TextContent == text || node.Children.Any(child => child.Tag == "#text" && child.TextContent == text))
                return true;
            foreach (var child in node.Children)
                if (Find(child, text, path)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    /// <summary>A builder's content that names no colour is drawn in the colour the part's own text is.</summary>
    [Fact]
    public void ABuildersContentWithNoColour_TakesThePartsColour()
    {
        using var plain = new ComponentTester(Theme);
        plain.Pump(new SegmentedControl(["Day", "Week"], 0, _ => { }));
        using var built = new ComponentTester(Theme);
        built.Pump(new SegmentedControl(["Day", "Week"], 0, _ => { }, segmentBuilder: Drawn));

        ColourOf(built, "drawn-0").Should().NotBeNull().And.Be(ColourOf(plain, "Day"));
        ColourOf(built, "drawn-1").Should().NotBeNull().And.Be(ColourOf(plain, "Week"));
    }

    [Fact]
    public void ASelectsBuiltOptions_TakeTheOptionsColour()
    {
        using var plain = new ComponentTester(Theme);
        plain.Pump(Grouped([], build: false));
        plain.Tap(SemanticRole.ComboBox, plain.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
        plain.Pump();
        using var built = new ComponentTester(Theme);
        built.Pump(Grouped([]));
        built.Tap(SemanticRole.ComboBox, built.Semantics.Single(stop => stop.Role == SemanticRole.ComboBox).Name);
        built.Pump();

        ColourOf(built, "drawn-0").Should().NotBeNull().And.Be(ColourOf(plain, "Lisboa"), "the chosen option's");
        ColourOf(built, "drawn-1").Should().NotBeNull().And.Be(ColourOf(plain, "Braga"), "and another's");
    }

    [Fact]
    public void WithNoBuilder_ASegmentedControlDrawsItsSegmentsAsText()
    {
        using var tester = new ComponentTester(Theme);
        tester.Pump(new SegmentedControl(["Day", "Week"], 0, _ => { }));

        tester.Html.Should().Contain("Week").And.NotContain("drawn-");
    }
}
